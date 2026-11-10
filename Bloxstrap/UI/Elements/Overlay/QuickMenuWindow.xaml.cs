using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using Bloxstrap.Integrations;
using Bloxstrap.UI.Elements.Overlay;

namespace Bloxstrap.UI.Elements.Overlay
{
    /// <summary>
    /// A small custom flyout menu opened with the double right shift hotkey.
    /// </summary>
    public partial class QuickMenuWindow : Window
    {
        private readonly Watcher _watcher;

        public QuickMenuWindow(Watcher watcher)
        {
            InitializeComponent();

            _watcher = watcher;
        }

        public void OpenAtCursor()
        {
            OverlayToggle.IsChecked = App.Settings.Prop.EnableGameOverlay;
            StatisticsToggle.IsChecked = App.Settings.Prop.EnableGameStatistics;

            bool inGame = _watcher.ActivityWatcher?.InGame == true;

            RestartRow.Visibility = inGame ? Visibility.Visible : Visibility.Collapsed;
            CloseRow.Visibility = inGame ? Visibility.Visible : Visibility.Collapsed;
            UserIdRow.Visibility = (inGame && !App.Settings.Prop.StreamerMode) ? Visibility.Visible : Visibility.Collapsed;

            PopulateRecentGames();

            // appear to the bottom-right of the cursor, clamped to the work area
            var cursor = System.Windows.Forms.Cursor.Position;

            Left = Math.Min(cursor.X + 10, SystemParameters.WorkArea.Right - Width - 4);
            Top = Math.Min(cursor.Y + 10, SystemParameters.WorkArea.Bottom - Height - 4);
            Left = Math.Max(SystemParameters.WorkArea.Left + 4, Left);
            Top = Math.Max(SystemParameters.WorkArea.Top + 4, Top);

            Show();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Hide();
        }

        private void PopulateRecentGames()
        {
            RecentGamesPanel.Children.Clear();

            var recent = App.Statistics.Prop.Games.Values
                .Where(x => x.PlaceId != 0)
                .OrderByDescending(x => x.LastPlayed)
                .Take(3)
                .ToList();

            if (!recent.Any())
                return;

            var header = new TextBlock
            {
                Text = Strings.ContextMenu_RecentGames,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 180)),
                FontSize = 11,
                Margin = new Thickness(2, 4, 0, 2),
            };
            RecentGamesPanel.Children.Add(header);

            foreach (var game in recent)
            {
                var row = new TextBlock
                {
                    Text = String.IsNullOrEmpty(game.Name) ? $"Place {game.PlaceId}" : game.Name,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 230, 230)),
                    FontSize = 12,
                    Cursor = Cursors.Hand,
                    Padding = new Thickness(2, 4, 2, 4),
                    ToolTip = Strings.Menu_Integrations_GameOverlay_Rejoin,
                };

                long placeId = game.PlaceId;

                row.MouseLeftButtonUp += (_, _) =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = $"roblox://experiences/start?placeId={placeId}",
                            UseShellExecute = true,
                        });
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException("QuickMenu::LaunchRecent", ex);
                    }

                    Hide();
                };

                RecentGamesPanel.Children.Add(row);
            }

            var separator = new Separator { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)), Margin = new Thickness(0, 6, 0, 6) };
            RecentGamesPanel.Children.Add(separator);
        }

        private void UserIdRow_Click(object sender, MouseButtonEventArgs e)
        {
            if (_watcher.ActivityWatcher is null)
                return;

            try
            {
                System.Windows.Clipboard.SetDataObject(_watcher.ActivityWatcher.Data.UserId.ToString());
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("QuickMenu::UserIdRow_Click", ex);
            }
        }

        private void OverlayToggle_Click(object sender, RoutedEventArgs e)
        {
            bool enabled = OverlayToggle.IsChecked == true;

            App.Settings.Prop.EnableGameOverlay = enabled;
            App.Settings.Save();

            // the overlay window only exists if the setting was on when the watcher started
            if (enabled && _watcher.Overlay is null && _watcher.ActivityWatcher is not null)
                _watcher.Overlay = new GameOverlayWindow(_watcher);

            _watcher.Overlay?.SetEnabled(enabled);
        }

        private void StatisticsToggle_Click(object sender, RoutedEventArgs e)
        {
            bool enabled = StatisticsToggle.IsChecked == true;

            App.Settings.Prop.EnableGameStatistics = enabled;
            App.Settings.Save();

            // start recording live if the tracker wasn't created at watcher startup
            if (enabled && _watcher.StatisticsTracker is null && _watcher.ActivityWatcher is not null)
                _watcher.StatisticsTracker = new StatisticsTracker(_watcher.ActivityWatcher);

            if (_watcher.StatisticsTracker is not null)
                _watcher.StatisticsTracker.Enabled = enabled;
        }

        private void RestartRow_Click(object sender, MouseButtonEventArgs e)
        {
            MessageBoxResult result = Frontend.ShowMessageBox(
                Strings.ContextMenu_RestartRobloxMessage,
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            _watcher.RestartRoblox();
            Hide();
        }

        private void CloseRow_Click(object sender, MouseButtonEventArgs e)
        {
            MessageBoxResult result = Frontend.ShowMessageBox(
                Strings.ContextMenu_CloseRobloxMessage,
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            _watcher.KillRobloxProcess();
            Hide();
        }
    }
}
