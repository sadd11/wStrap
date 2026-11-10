﻿using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

using Bloxstrap.Integrations;
using Bloxstrap.UI.Elements.Overlay;

namespace Bloxstrap.UI.Elements.ContextMenu
{
    /// <summary>
    /// Interaction logic for NotifyIconMenu.xaml
    /// </summary>
    public partial class MenuContainer
    {
        // i wouldve gladly done this as mvvm but turns out that data binding just does not work with menuitems for some reason so idk this sucks

        private readonly Watcher _watcher;

        private ActivityWatcher? _activityWatcher => _watcher.ActivityWatcher;

        private ServerInformation? _serverInformationWindow;

        private ServerHistory? _gameHistoryWindow;

        #region Right Shift hotkey

        // double-tapping the right shift key opens this menu at the cursor
        // a low-level keyboard hook is used in pass-through mode, so single
        // presses keep working normally (e.g. for typing capital letters)

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int VK_RSHIFT = 0xA1;

        private const double DoubleTapWindowMs = 400;
        private const double AutoRepeatThresholdMs = 80;

        private IntPtr _hookHandle = IntPtr.Zero;
        private LowLevelKeyboardProc? _hookProc;
        private DateTime _lastRightShiftDown = DateTime.MinValue;
        private DateTime _lastRightShiftTap = DateTime.MinValue;

        private QuickMenuWindow? _quickMenu;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        private void InstallRightShiftHook()
        {
            const string LOG_IDENT = "MenuContainer::InstallRightShiftHook";

            _hookProc = RightShiftHookProc;
            _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);

            if (_hookHandle == IntPtr.Zero)
                App.Logger.WriteLine(LOG_IDENT, "Failed to install keyboard hook");
            else
                App.Logger.WriteLine(LOG_IDENT, "Keyboard hook installed, double tap right shift to open the menu");
        }

        private IntPtr RightShiftHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && wParam.ToInt32() == WM_KEYDOWN)
                {
                    var hookData = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                    if (hookData.vkCode == VK_RSHIFT)
                    {
                        var now = DateTime.UtcNow;

                        // ignore auto-repeat keydowns while the key is held
                        bool isRepeat = (now - _lastRightShiftDown).TotalMilliseconds < AutoRepeatThresholdMs;
                        _lastRightShiftDown = now;

                        if (!isRepeat)
                        {
                            if ((now - _lastRightShiftTap).TotalMilliseconds <= DoubleTapWindowMs)
                            {
                                _lastRightShiftTap = DateTime.MinValue;
                                Dispatcher.BeginInvoke(OpenMenuAtCursor);
                            }
                            else
                            {
                                _lastRightShiftTap = now;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // never let the hook throw, it runs for every keypress on the system
            }

            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private void OpenMenuAtCursor()
        {
            // custom quick menu instead of the plain context menu
            if (_quickMenu is null)
            {
                _quickMenu = new QuickMenuWindow(_watcher);
                _quickMenu.Closed += (_, _) => _quickMenu = null;
            }

            _quickMenu.OpenAtCursor();
        }

        #endregion

        public MenuContainer(Watcher watcher)
        {
            InitializeComponent();

            _watcher = watcher;

            if (_activityWatcher is not null)
            {
                _activityWatcher.OnLogOpen += ActivityWatcher_OnLogOpen;
                _activityWatcher.OnGameJoin += ActivityWatcher_OnGameJoin;
                _activityWatcher.OnGameLeave += ActivityWatcher_OnGameLeave;

                if (!App.Settings.Prop.UseDisableAppPatch)
                    GameHistoryMenuItem.Visibility = Visibility.Visible;

            if (App.Cookies.Loaded && !App.Settings.Prop.StreamerMode)
                FriendsMenuItem.Visibility = Visibility.Visible;
            }

            if (_watcher.RichPresence is not null)
                RichPresenceMenuItem.Visibility = Visibility.Visible;

            OverlayMenuItem.IsChecked = App.Settings.Prop.EnableGameOverlay;
            StatisticsMenuItem.IsChecked = App.Settings.Prop.EnableGameStatistics;

            VersionTextBlock.Text = $"{App.ProjectName} v{App.Version}";

            InstallRightShiftHook();
        }

        public void ShowServerInformationWindow()
        {
            if (_serverInformationWindow is null)
            {
                _serverInformationWindow = new(_watcher);
                _serverInformationWindow.Closed += (_, _) => _serverInformationWindow = null;
            }

            if (!_serverInformationWindow.IsVisible)
                _serverInformationWindow.ShowDialog();
            else
                _serverInformationWindow.Activate();
        }

        public void ActivityWatcher_OnLogOpen(object? sender, EventArgs e) => 
            Dispatcher.Invoke(() => LogTracerMenuItem.Visibility = Visibility.Visible);

        public void ActivityWatcher_OnGameJoin(object? sender, EventArgs e)
        {
            if (_activityWatcher is null)
                return;

            Dispatcher.Invoke(() => {
                if (_activityWatcher.Data.ServerType == ServerType.Public)
                    InviteDeeplinkMenuItem.Visibility = Visibility.Visible;

                ServerDetailsMenuItem.Visibility = Visibility.Visible;
                RestartRobloxMenuItem.Visibility = Visibility.Visible;
            });
        }

        public void ActivityWatcher_OnGameLeave(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() => {
                InviteDeeplinkMenuItem.Visibility = Visibility.Collapsed;
                ServerDetailsMenuItem.Visibility = Visibility.Collapsed;
                RestartRobloxMenuItem.Visibility = Visibility.Collapsed;

                _serverInformationWindow?.Close();
            });
        }

        private void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            // this is an awful hack lmao im so sorry to anyone who reads this
            // this is done to register the context menu wrapper as a tool window so it doesnt appear in the alt+tab switcher
            // https://stackoverflow.com/a/551847/11852173

            HWND hWnd = (HWND)new WindowInteropHelper(this).Handle;

            int exStyle = PInvoke.GetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            exStyle |= 0x00000080; //NativeMethods.WS_EX_TOOLWINDOW;
            PInvoke.SetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (_hookHandle != IntPtr.Zero)
                UnhookWindowsHookEx(_hookHandle);

            App.Logger.WriteLine("MenuContainer::Window_Closed", "Context menu container closed");
        }

        private void RichPresenceMenuItem_Click(object sender, RoutedEventArgs e) => _watcher.RichPresence?.SetVisibility(((MenuItem)sender).IsChecked);

        private void OverlayMenuItem_Click(object sender, RoutedEventArgs e)
        {
            bool enabled = ((MenuItem)sender).IsChecked;

            App.Settings.Prop.EnableGameOverlay = enabled;
            App.Settings.Save();

            // the overlay window only exists if the setting was on when the watcher started
            if (enabled && _watcher.Overlay is null && _activityWatcher is not null)
                _watcher.Overlay = new GameOverlayWindow(_watcher);

            _watcher.Overlay?.SetEnabled(enabled);
        }

        private void StatisticsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            bool enabled = ((MenuItem)sender).IsChecked;

            App.Settings.Prop.EnableGameStatistics = enabled;
            App.Settings.Save();

            // start recording live if the tracker wasn't created at watcher startup
            if (enabled && _watcher.StatisticsTracker is null && _activityWatcher is not null)
                _watcher.StatisticsTracker = new StatisticsTracker(_activityWatcher);

            if (_watcher.StatisticsTracker is not null)
                _watcher.StatisticsTracker.Enabled = enabled;
        }

        private void InviteDeeplinkMenuItem_Click(object sender, RoutedEventArgs e) => Clipboard.SetDataObject(_activityWatcher?.Data.GetInviteDeeplink());

        private void ServerDetailsMenuItem_Click(object sender, RoutedEventArgs e) => ShowServerInformationWindow();

        private void LogTracerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string? location = _activityWatcher?.LogLocation;

            if (location is not null)
                Utilities.ShellExecute(location);
        }

        private void CloseRobloxMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = Frontend.ShowMessageBox(
                Strings.ContextMenu_CloseRobloxMessage,
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            _watcher.KillRobloxProcess();
        }

        private void RestartRobloxMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = Frontend.ShowMessageBox(
                Strings.ContextMenu_RestartRobloxMessage,
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            _watcher.RestartRoblox();
        }

        private void FriendsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            new FriendsWindow().Show();
        }

        private void JoinLastServerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_activityWatcher is null)
                throw new ArgumentNullException(nameof(_activityWatcher));

            if (_gameHistoryWindow is null)
            {
                _gameHistoryWindow = new(_activityWatcher);
                _gameHistoryWindow.Closed += (_, _) => _gameHistoryWindow = null;
            }

            if (!_gameHistoryWindow.IsVisible)
                _gameHistoryWindow.ShowDialog();
            else
                _gameHistoryWindow.Activate();
        }
    }
}
