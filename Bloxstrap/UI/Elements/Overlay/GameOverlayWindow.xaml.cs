using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using Bloxstrap.Integrations;
using Bloxstrap.UI.Elements.ContextMenu;
using Wpf.Ui.Common;

namespace Bloxstrap.UI.Elements.Overlay
{
    /// <summary>
    /// A small always-on-top draggable pill overlay shown while playing, in a minimal black and white Roblox style.
    /// Shows live server ping and session time. Left click rejoins the last server, right click opens server details.
    /// </summary>
    public partial class GameOverlayWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private const double ClickThresholdPx = 6;

        private readonly Watcher _watcher;

        private readonly DispatcherTimer _timer;

        private bool _closedByUser = false;

        private System.Threading.Timer? _pingTimer;
        private string? _pingHost;

        private string? _gameName;
        private string? _serverRegion;

        private bool _expanded = false;
        private IntPtr _hwnd = IntPtr.Zero;
        private bool _collapseFinalized = true;
        private DispatcherTimer? _collapseFallback;

        private static GameOverlayWindow? _activeInstance;

        private readonly double _scale;

        private long _pingSum = 0;
        private int _pingCount = 0;
        private int? _lastLatency = null;

        private ActivityWatcher? _activityWatcher => _watcher.ActivityWatcher;

        public GameOverlayWindow(Watcher watcher)
        {
            const string LOG_IDENT = "GameOverlayWindow";

            InitializeComponent();

            // never allow two overlay instances
            if (_activeInstance is not null)
                _activeInstance.ForceClose();

            _activeInstance = this;
            Closed += (_, _) =>
            {
                if (_activeInstance == this)
                    _activeInstance = null;
            };

            _watcher = watcher;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) => OnTimerTick();

            // pill darkness is user-configurable (blended towards black)
            byte shade = (byte)(0x14 * Math.Clamp((int)App.Settings.Prop.GameOverlayOpacity, 40, 240) / 200);
            OverlayBorder.Background = new SolidColorBrush(Color.FromRgb(shade, shade, shade));

            // size setting scales the whole pill
            _scale = App.Settings.Prop.GameOverlaySize switch
            {
                GameOverlaySize.Small => 0.85,
                GameOverlaySize.Large => 1.2,
                _ => 1.0
            };
            OverlayBorder.LayoutTransform = new ScaleTransform(_scale, _scale);

            // super-mini mode: ping only, no icon and no clock

            if (App.Settings.Prop.GameOverlaySuperMini)
            {
                GameIcon.Visibility = Visibility.Collapsed;
                TimeText.Visibility = Visibility.Collapsed;
            }

            if (App.Settings.Prop.GameOverlayShowClock)
                ClockText.Visibility = Visibility.Visible;

            UpdateTimerText();

            if (_activityWatcher is not null)
            {
                _activityWatcher.OnGameJoin += (_, _) => Dispatcher.Invoke(() =>
                {
                    if (_closedByUser)
                        return;

                    RefreshGameInfo();
                    StartPinging();
                    _timer.Start();
                    Show();
                });

                // keep the pill visible after leaving a game, so it can be used to rejoin
                _activityWatcher.OnGameLeave += (_, _) => Dispatcher.Invoke(() =>
                {
                    StopPinging();
                    _timer.Stop();
                });
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, "No activity watcher, overlay will stay hidden");
            }

            SetDefaultPosition();

            App.Logger.WriteLine(LOG_IDENT, "Overlay initialized");
        }

        private void Window_SourceInitialized(object? sender, EventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            IntPtr hwnd = _hwnd;

            // hide from the alt-tab switcher and stop the overlay from stealing focus
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

            TryRoundCorners(hwnd);
        }

        private void OnTimerTick()
        {
            UpdateTimerText();
            CheckAutoHide();

            if (ClockText.Visibility == Visibility.Visible)
                ClockText.Text = DateTime.Now.ToString("HH:mm");
        }

        private void CheckAutoHide()
        {
            if (!App.Settings.Prop.GameOverlayAutoHide || _hwnd == IntPtr.Zero)
                return;

            bool minimized = IsIconic(_watcher.RobloxWindowHandle);

            if (minimized && IsVisible)
                Hide();
            else if (!minimized && !IsVisible && !_closedByUser)
                Show();
        }

        private void TryRoundCorners(IntPtr hwnd)
        {
            int preference = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }

        private void SetDefaultPosition()
        {
            string? saved = App.State.Prop.GameOverlayPosition;

            if (!String.IsNullOrEmpty(saved))
            {
                string[] parts = saved.Split(',');

                if (parts.Length == 2 &&
                    double.TryParse(parts[0], out double left) &&
                    double.TryParse(parts[1], out double top))
                {
                    // make sure the saved position is still on screen
                    if (left >= SystemParameters.VirtualScreenLeft && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
                        top >= SystemParameters.VirtualScreenTop && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
                    {
                        Left = left;
                        Top = top;
                        return;
                    }
                }
            }

            // default to top-center of the work area, centered on the pill
            Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - CompactWidth * _scale) / 2;
            Top = SystemParameters.WorkArea.Top + 12;
        }

        /// <summary>
        /// Live enable/disable from the tray menu and quick menu.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                _closedByUser = false;

                if (_activityWatcher?.InGame == true)
                {
                    RefreshGameInfo();
                    StartPinging();
                    _timer.Start();
                }
                else
                {
                    GameIcon.Visibility = Visibility.Collapsed;
                    GameIcon.Source = null;
                    UpdatePingDisplay(null, false);
                }

                UpdateTimerText();
                Show();
            }
            else
            {
                _closedByUser = true;
                StopPinging();
                _timer.Stop();
                ResetToCompact();
                Hide();
            }
        }

        private void RefreshGameInfo()
        {
            if (_activityWatcher is null)
                return;

            ActivityData data = _activityWatcher.Data;

            GameIcon.Visibility = Visibility.Collapsed;
            GameIcon.Source = null;
            _gameName = "Roblox";
            _serverRegion = null;
            UpdateTooltip();

            UpdateTimerText();

            if (data.UniverseId == 0)
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    UniverseDetails? details = UniverseDetails.LoadFromCache(data.UniverseId);

                    if (details is null)
                    {
                        await UniverseDetails.FetchSingle(data.UniverseId);
                        details = UniverseDetails.LoadFromCache(data.UniverseId);
                    }

                    if (details is null)
                        return;

                    Dispatcher.Invoke(() =>
                    {
                        _gameName = details.Data.Name;

                        if (!String.IsNullOrEmpty(details.Thumbnail.ImageUrl))
                            LoadGameIcon(details.Thumbnail.ImageUrl);

                        UpdateTooltip();
                    });
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("GameOverlayWindow::RefreshGameInfo", ex);
                }
            });

            // server region, shown on the pill and in the tooltip
            _ = Task.Run(async () =>
            {
                try
                {
                    string? region = await data.QueryServerLocation();

                    Dispatcher.Invoke(() =>
                    {
                        _serverRegion = region;
                        UpdateTooltip();

                        string shortRegion = region?.Split(',').LastOrDefault()?.Trim() ?? "";

                        if (String.IsNullOrEmpty(shortRegion))
                        {
                            ExpRegionText.Visibility = Visibility.Collapsed;
                            return;
                        }

                        if (shortRegion.Length > 10)
                            shortRegion = shortRegion[..9] + "…";

                        ExpRegionText.Text = "· " + shortRegion;
                        ExpRegionText.Visibility = Visibility.Visible;
                    });
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("GameOverlayWindow::RefreshGameInfo", ex);
                }
            });
        }

        private void UpdateTooltip()
        {
            string tooltip = _gameName ?? "Roblox";

            if (!String.IsNullOrEmpty(_serverRegion))
                tooltip += $"\n{_serverRegion}";

            if (App.Statistics.Prop.DailySeconds.TryGetValue(DateTime.Now.ToString("yyyy-MM-dd"), out long todaySeconds) && todaySeconds > 0)
                tooltip += $"\n{string.Format(Strings.Menu_Integrations_GameOverlay_Today, Bloxstrap.UI.ViewModels.Settings.GamesViewModel.FormatPlaytime(todaySeconds))}";

            if (_pingCount > 0)
                tooltip += $"\n{string.Format(Strings.Menu_Integrations_GameOverlay_AvgPing, _pingSum / _pingCount)}";

            tooltip += $"\n{Strings.Menu_Integrations_GameOverlay_RejoinHint}";

            ToolTip = tooltip;
        }

        private void RejoinLastServer()
        {
            const string LOG_IDENT = "GameOverlayWindow::RejoinLastServer";

            if (_activityWatcher is null)
                return;

            ActivityData? activity = _activityWatcher.InGame ? _activityWatcher.Data : _activityWatcher.History.FirstOrDefault();

            if (activity is null || activity.PlaceId == 0)
                return;

            string deeplink = activity.GetInviteDeeplink(false, true);

            App.Logger.WriteLine(LOG_IDENT, $"Rejoining last server (placeId={activity.PlaceId})");

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = deeplink,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        private void ShowServerInformation()
        {
            var window = new ServerInformation(_watcher);
            window.ShowDialog();
        }

        private void LoadGameIcon(string url)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    byte[] bytes = await App.HttpClient.GetByteArrayAsync(url);

                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = new MemoryStream(bytes);
                    image.EndInit();
                    image.Freeze();

                    Dispatcher.Invoke(() =>
                    {
                        GameIcon.Source = image;
                        GameIcon.Visibility = Visibility.Visible;
                    });
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("GameOverlayWindow::LoadGameIcon", ex);
                }
            });
        }

        private void UpdateTimerText()
        {
            string text = "0:00";

            if (_activityWatcher is not null)
            {
                DateTime joined = _activityWatcher.Data.RootActivity?.TimeJoined ?? _activityWatcher.Data.TimeJoined;

                if (joined != default)
                {
                    TimeSpan elapsed = DateTime.Now - joined;

                    text = elapsed.TotalHours >= 1
                        ? $"{(int)elapsed.TotalHours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}"
                        : $"{elapsed.Minutes}:{elapsed.Seconds:D2}";
                }
            }

            TimeText.Text = text;
            ExpTimeText.Text = text;
        }

        private void SavePosition()
        {
            App.State.Prop.GameOverlayPosition = $"{Left:0},{Top:0}";
            App.State.Save();
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_expanded)
            {
                // clicks on the action rows handle themselves, anything else collapses the menu
                if (!IsInsideActionRow(e.OriginalSource as DependencyObject))
                    CollapseMenu();

                return;
            }

            // remember where the drag started so we can tell a click from a drag
            Point origin = e.GetPosition(this);
            Point screenOrigin = PointToScreen(origin);

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // can be thrown if the mouse button was released before DragMove ran, safe to ignore
                return;
            }

            // DragMove returns once the button is released
            Point end = new(System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y);
            double delta = Math.Abs(end.X - screenOrigin.X) + Math.Abs(end.Y - screenOrigin.Y);

            SavePosition();

            if (delta < ClickThresholdPx)
                ExpandMenu();
        }

        private bool IsInsideActionRow(DependencyObject? source)
        {
            while (source is not null)
            {
                if (source == RejoinRow || source == ScreenshotRow || source == CopyInviteRow ||
                    source == InfoRow || source == RestartRow ||
                    source == OverlayToggle || source == StatisticsToggle)
                    return true;

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        #region Expand / collapse

        private const double CompactWidth = 172;
        private const double CompactHeight = 40;
        private const double ExpandedWidth = 232;
        private const double ExpandedHeight = 330;

        private void ExpandMenu()
        {
            if (_expanded)
                return;

            _expanded = true;

            OverlayToggle.IsChecked = App.Settings.Prop.EnableGameOverlay;
            StatisticsToggle.IsChecked = App.Settings.Prop.EnableGameStatistics;

            bool hasServer = _activityWatcher is not null &&
                ((_activityWatcher.InGame && _activityWatcher.Data.PlaceId != 0) || _activityWatcher.History.Any(x => x.PlaceId != 0));

            RejoinRow.Visibility = hasServer ? Visibility.Visible : Visibility.Collapsed;

            // the border is scaled by a LayoutTransform, so animate in logical units and check the screen in scaled ones
            double targetWidth = ExpandedWidth;
            double targetHeight = ExpandedHeight;
            double visualWidth = Math.Round(targetWidth * _scale);
            double visualHeight = Math.Round(targetHeight * _scale);

            // a collapse that is still running must not shrink the menu we are opening now
            _collapseFallback?.Stop();
            _collapseFinalized = true;

            // keep the expanded menu on screen
            if (Top + visualHeight > SystemParameters.WorkArea.Bottom - 4)
                Top = SystemParameters.WorkArea.Bottom - visualHeight - 4;

            if (Left + visualWidth > SystemParameters.WorkArea.Right - 4)
                Left = SystemParameters.WorkArea.Right - visualWidth - 4;

            // a little overshoot gives the expansion a springy feel
            var ease = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(300);

            // deterministic state switch - only one panel is ever visible
            CompactPanel.Visibility = Visibility.Collapsed;
            ExpandedPanel.Visibility = Visibility.Visible;

            OverlayBorder.BeginAnimation(WidthProperty, new DoubleAnimation(OverlayBorder.ActualWidth, targetWidth, duration) { EasingFunction = ease });
            OverlayBorder.BeginAnimation(HeightProperty, new DoubleAnimation(OverlayBorder.ActualHeight, targetHeight, duration) { EasingFunction = ease });

            OverlayBorder.CornerRadius = new CornerRadius(16);

            // stagger the menu rows in
            UIElement[] rows = { RejoinRow, ScreenshotRow, CopyInviteRow, InfoRow, RestartRow, OverlayToggleRow, StatisticsToggleRow };

            for (int i = 0; i < rows.Length; i++)
            {
                var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)) { BeginTime = TimeSpan.FromMilliseconds(110 + i * 32) };
                rows[i].BeginAnimation(OpacityProperty, anim);
            }
        }

        private void CollapseMenu()
        {
            if (!_expanded)
                return;

            _expanded = false;
            _collapseFinalized = false;

            // swap panels right away so the compact pill is always in a known state
            ExpandedPanel.Visibility = Visibility.Collapsed;
            CompactPanel.Visibility = Visibility.Visible;
            OverlayBorder.CornerRadius = new CornerRadius(12);

            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            var duration = TimeSpan.FromMilliseconds(200);

            // animate the pill itself - the window is SizeToContent and follows it
            var widthAnim = new DoubleAnimation(OverlayBorder.ActualWidth, CompactWidth, duration) { EasingFunction = ease };
            var heightAnim = new DoubleAnimation(OverlayBorder.ActualHeight, CompactHeight, duration) { EasingFunction = ease };

            heightAnim.Completed += (_, _) => FinishCollapse();

            OverlayBorder.BeginAnimation(WidthProperty, widthAnim);
            OverlayBorder.BeginAnimation(HeightProperty, heightAnim);

            // safety net in case the completed callback never fires
            _collapseFallback?.Stop();
            _collapseFallback = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
            _collapseFallback.Tick += (_, _) =>
            {
                _collapseFallback?.Stop();
                FinishCollapse();
            };
            _collapseFallback.Start();
        }

        private void FinishCollapse()
        {
            // already done, or the menu was opened again while the collapse was running
            if (_collapseFinalized || _expanded)
                return;

            _collapseFinalized = true;
            _collapseFallback?.Stop();

            // drop every animation so nothing keeps holding the old size, then set the exact pill size
            OverlayBorder.BeginAnimation(WidthProperty, null);
            OverlayBorder.BeginAnimation(HeightProperty, null);
            OverlayBorder.Width = CompactWidth;
            OverlayBorder.Height = CompactHeight;
            OverlayBorder.CornerRadius = new CornerRadius(12);

            SavePosition();
        }

        /// <summary>
        /// Instantly returns to the compact pill without animation.
        /// </summary>
        private void ResetToCompact()
        {
            _collapseFallback?.Stop();
            _expanded = false;
            _collapseFinalized = true;

            ExpandedPanel.Visibility = Visibility.Collapsed;
            CompactPanel.Visibility = Visibility.Visible;

            OverlayBorder.BeginAnimation(WidthProperty, null);
            OverlayBorder.BeginAnimation(HeightProperty, null);
            OverlayBorder.Width = CompactWidth;
            OverlayBorder.Height = CompactHeight;
            OverlayBorder.CornerRadius = new CornerRadius(12);
        }

        private void ForceClose()
        {
            _closedByUser = true;
            StopPinging();
            _timer.Stop();
            Close();
        }

        #endregion

        #region Expanded menu actions

        private void RejoinRow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CollapseMenu();
            RejoinLastServer();
        }

        private void RestartRow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CollapseMenu();

            MessageBoxResult result = Frontend.ShowMessageBox(
                Strings.ContextMenu_RestartRobloxMessage,
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            _watcher.RestartRoblox();
        }

        private void InfoRow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CollapseMenu();
            ShowServerInformation();
        }

        private void ScreenshotRow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CollapseMenu();

            _ = Task.Run(() =>
            {
                string? path = Bloxstrap.Utility.GameScreenshot.Capture(_watcher.RobloxWindowHandle);

                Dispatcher.Invoke(() =>
                {
                    if (path is null)
                    {
                        _watcher.ShowAlert("wStrap", Strings.Menu_Integrations_GameOverlay_ScreenshotFailed, 5);
                        return;
                    }

                    // copy the image to the clipboard as well
                    try
                    {
                        var image = new System.Drawing.Bitmap(path);
                        System.Windows.Clipboard.SetDataObject(image, true);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException("GameOverlayWindow::ScreenshotRow_Click", ex);
                    }

                    _watcher.ShowAlert("wStrap", string.Format(Strings.Menu_Integrations_GameOverlay_ScreenshotSaved, path), 6);
                });
            });
        }

        private void CopyInviteRow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CollapseMenu();

            if (_activityWatcher is null)
                return;

            ActivityData? activity = _activityWatcher.InGame ? _activityWatcher.Data : _activityWatcher.History.FirstOrDefault();

            if (activity is null || activity.PlaceId == 0)
                return;

            try
            {
                System.Windows.Clipboard.SetDataObject(activity.GetInviteDeeplink());
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("GameOverlayWindow::CopyInviteRow_Click", ex);
            }
        }

        private void OverlayToggle_Click(object sender, RoutedEventArgs e)
        {
            bool enabled = OverlayToggle.IsChecked == true;

            App.Settings.Prop.EnableGameOverlay = enabled;
            App.Settings.Save();

            // the overlay window only exists if the setting was on when the watcher started
            if (enabled && _watcher.Overlay is null && _activityWatcher is not null)
                _watcher.Overlay = new GameOverlayWindow(_watcher);

            _watcher.Overlay?.SetEnabled(enabled);
        }

        private void StatisticsToggle_Click(object sender, RoutedEventArgs e)
        {
            bool enabled = StatisticsToggle.IsChecked == true;

            App.Settings.Prop.EnableGameStatistics = enabled;
            App.Settings.Save();

            if (enabled && _watcher.StatisticsTracker is null && _activityWatcher is not null)
                _watcher.StatisticsTracker = new StatisticsTracker(_activityWatcher);

            if (_watcher.StatisticsTracker is not null)
                _watcher.StatisticsTracker.Enabled = enabled;
        }

        #endregion

        private void Window_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ShowServerInformation();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _closedByUser = true;
            _timer.Stop();
            Hide();
        }

        #region Server ping

        /// <summary>
        /// Updates the wifi icon, ping number and the compact circle's latency dot.
        /// </summary>
        private void UpdatePingDisplay(int? latency, bool pinging)
        {
            Color dotColor;

            if (latency is int l)
            {
                PingText.Text = $"{l} ms";
                ExpPingText.Text = $"{l} ms";
                ExpWifiIcon.Opacity = 1;
                // note: the fluent glyphs are named backwards - Wifi124 is full signal, Wifi424 is one bar
                var symbol = l <= 50 ? SymbolRegular.Wifi124
                           : l <= 100 ? SymbolRegular.Wifi224
                           : l <= 200 ? SymbolRegular.Wifi324
                           : SymbolRegular.Wifi424;
                WifiIcon.Symbol = symbol;
                ExpWifiIcon.Symbol = symbol;
                dotColor = l <= 50 ? Color.FromRgb(76, 195, 138)
                         : l <= 100 ? Color.FromRgb(242, 199, 68)
                         : l <= 200 ? Color.FromRgb(233, 117, 72)
                         : Color.FromRgb(224, 108, 92);
            }
            else if (pinging)
            {
                PingText.Text = "…";
                ExpPingText.Text = "…";
                ExpWifiIcon.Opacity = 0.8;
                ExpWifiIcon.Symbol = SymbolRegular.WifiWarning24;
                WifiIcon.Symbol = SymbolRegular.WifiWarning24;
                dotColor = Colors.White;
            }
            else
            {
                PingText.Text = "—";
                ExpPingText.Text = "—";
                ExpWifiIcon.Opacity = 0.4;
                ExpWifiIcon.Symbol = SymbolRegular.WifiOff24;
                WifiIcon.Symbol = SymbolRegular.WifiOff24;
                dotColor = Color.FromRgb(128, 128, 128);
            }

            WifiIcon.Foreground = new SolidColorBrush(dotColor);
            WifiIcon.Opacity = 1;
        }

        private void StartPinging()
        {
            const string LOG_IDENT = "GameOverlayWindow::StartPinging";

            ActivityData? data = _activityWatcher?.Data;

            if (data is null || !data.MachineAddressValid)
            {
                _pingHost = null;
                UpdatePingDisplay(null, false);
                return;
            }

            _pingHost = data.MachineAddress;
            UpdatePingDisplay(null, true);

            _pingSum = 0;
            _pingCount = 0;
            _lastLatency = null;

            _pingTimer?.Dispose();
            int interval = Math.Max(1, App.Settings.Prop.GameOverlayPingInterval) * 1000;
            _pingTimer = new System.Threading.Timer(async _ => await PingLoop(), null, 0, interval);

            App.Logger.WriteLine(LOG_IDENT, $"Pinging game server {_pingHost}");
        }

        private void StopPinging()
        {
            _pingHost = null;
            _pingTimer?.Dispose();
            _pingTimer = null;
            UpdatePingDisplay(null, false);
        }

        private async Task PingLoop()
        {
            string? host = _pingHost;

            int? latency = null;

            if (!String.IsNullOrEmpty(host))
            {
                try
                {
                    using var ping = new Ping();
                    PingReply reply = await ping.SendPingAsync(host, 1500);

                    if (reply.Status == IPStatus.Success)
                        latency = (int)reply.RoundtripTime;
                }
                catch (Exception)
                {
                    // network hiccups are expected, the next tick will retry
                }
            }

            try
            {
                Dispatcher.Invoke(() =>
                {
                    UpdatePingDisplay(latency, true);

                    if (latency is int l)
                    {
                        _pingSum += l;
                        _pingCount++;

                        // blink the icons when the latency jumps sharply
                        if (_lastLatency is int prev && Math.Abs(l - prev) > 80)
                        {
                            var pulse = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                            pulse.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                            pulse.KeyFrames.Add(new LinearDoubleKeyFrame(0.15, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
                            pulse.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))));

                            WifiIcon.BeginAnimation(OpacityProperty, pulse);
                            ExpWifiIcon.BeginAnimation(OpacityProperty, pulse.Clone());
                        }

                        _lastLatency = l;
                    }
                });
            }
            catch (Exception)
            {
                // the overlay may be gone by the time the ping returns
            }
        }

        #endregion

        [StructLayout(LayoutKind.Sequential)]
        private struct ACCENT_POLICY
        {
            public int AccentState;
            public int AccentFlags;
            public uint GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWCOMPOSITIONATTRIBDATA
        {
            public int Attrib;
            public IntPtr Data;
            public int SizeOfData;
        }

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBDATA data);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
