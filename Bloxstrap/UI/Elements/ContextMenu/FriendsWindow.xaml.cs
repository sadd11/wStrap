using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Bloxstrap.Utility;

namespace Bloxstrap.UI.Elements.ContextMenu
{
    public partial class FriendsWindow
    {
        public FriendsWindow()
        {
            InitializeComponent();
            Load();
        }

        private async void Load()
        {
            try
            {
                var friends = await FriendsService.GetOnlineFriends();

                LoadingRing.Visibility = Visibility.Collapsed;

                if (!friends.Any())
                {
                    ErrorText.Text = Strings.Friends_Empty;
                    ErrorText.Visibility = Visibility.Visible;
                    return;
                }

                foreach (var friend in friends)
                {
                    var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

                    stack.Children.Add(new TextBlock
                    {
                        Text = friend.DisplayName,
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                    });

                    string status = friend.PresenceType == 2 ? Strings.Friends_Status_InGame
                                  : friend.PresenceType == 1 ? Strings.Friends_Status_Online
                                  : "";

                    if (!String.IsNullOrEmpty(status))
                        stack.Children.Add(new TextBlock
                        {
                            Text = status,
                            FontSize = 11,
                            Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                        });

                    row.Children.Add(stack);
                    Grid.SetColumn(stack, 0);

                    if (friend.PresenceType == 2 && friend.PlaceId is long placeId && placeId > 0)
                    {
                        var joinButton = new Wpf.Ui.Controls.Button
                        {
                            Content = Strings.Friends_Join,
                            Appearance = Wpf.Ui.Common.ControlAppearance.Primary,
                        };

                        joinButton.Click += (_, _) =>
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
                                App.Logger.WriteException("FriendsWindow", ex);
                            }
                        };

                        Grid.SetColumn(joinButton, 1);
                        row.Children.Add(joinButton);
                    }

                    FriendsList.Items.Add(row);
                }

                FriendsList.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("FriendsWindow::Load", ex);

                LoadingRing.Visibility = Visibility.Collapsed;
                ErrorText.Text = Strings.Friends_LoadFailed;
                ErrorText.Visibility = Visibility.Visible;
            }
        }
    }
}
