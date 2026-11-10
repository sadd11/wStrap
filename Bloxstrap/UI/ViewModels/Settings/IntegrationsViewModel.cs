using Bloxstrap.UI.Elements.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Wpf.Ui.Common.Interfaces;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class IntegrationsViewModel : NotifyPropertyChangedViewModel, INavigationAware
    {

        public bool BorderlessFullscreenAllowed => App.Settings.Prop.EnableWindowManipulation;

        public bool EnableRobloxBackgroundApp
        {
            get => App.Settings.Prop.EnableRobloxBackgroundApp;
            set => App.Settings.Prop.EnableRobloxBackgroundApp = value;
        }

        public IReadOnlyCollection<RobloxTheme> RobloxThemes { get; } = RobloxThemeEx.Selections;

        public RobloxTheme RobloxTheme
        {
            get => App.Settings.Prop.RobloxTheme;
            set => App.Settings.Prop.RobloxTheme = value;
        }

        public ICommand AddIntegrationCommand => new RelayCommand(AddIntegration);

        public ICommand DeleteIntegrationCommand => new RelayCommand(DeleteIntegration);

        public ICommand BrowseIntegrationLocationCommand => new RelayCommand(BrowseIntegrationLocation);

        public void OnNavigatedTo()
        {
            OnPropertyChanged(nameof(BorderlessFullscreenAllowed));
            OnPropertyChanged(nameof(EnableFakeBorderlessFullscreen));
        }

        public void OnNavigatedFrom() { }

        private void AddIntegration()
        {
            CustomIntegrations.Add(new CustomIntegration()
            {
                Name = Strings.Menu_Integrations_Custom_NewIntegration
            });

            SelectedCustomIntegrationIndex = CustomIntegrations.Count - 1;

            OnPropertyChanged(nameof(SelectedCustomIntegrationIndex));
            OnPropertyChanged(nameof(IsCustomIntegrationSelected));
        }

        private void DeleteIntegration()
        {
            if (SelectedCustomIntegration is null)
                return;

            CustomIntegrations.Remove(SelectedCustomIntegration);

            if (CustomIntegrations.Count > 0)
            {
                SelectedCustomIntegrationIndex = CustomIntegrations.Count - 1;
                OnPropertyChanged(nameof(SelectedCustomIntegrationIndex));
            }

            OnPropertyChanged(nameof(IsCustomIntegrationSelected));
        }

        private void BrowseIntegrationLocation()
        {
            if (SelectedCustomIntegration is null)
                return;

            var dialog = new OpenFileDialog
            {
                Filter = $"{Strings.Menu_AllFiles}|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            SelectedCustomIntegration.Name = dialog.SafeFileName;
            SelectedCustomIntegration.Location = dialog.FileName;
            OnPropertyChanged(nameof(SelectedCustomIntegration));
        }

        public bool ActivityTrackingEnabled
        {
            get => App.Settings.Prop.EnableActivityTracking;
            set
            {
                App.Settings.Prop.EnableActivityTracking = value;

                if (!value)
                {
                    ShowServerDetailsEnabled = value;
                    DisableAppPatchEnabled = value;
                    DiscordActivityEnabled = value;
                    DiscordActivityJoinEnabled = value;

                    OnPropertyChanged(nameof(ShowServerDetailsEnabled));
                    OnPropertyChanged(nameof(DisableAppPatchEnabled));
                    OnPropertyChanged(nameof(DiscordActivityEnabled));
                    OnPropertyChanged(nameof(DiscordActivityJoinEnabled));
                }
            }
        }

        public bool ShowServerDetailsEnabled
        {
            get => App.Settings.Prop.ShowServerDetails;
            set => App.Settings.Prop.ShowServerDetails = value;
        }

        public bool DiscordActivityEnabled
        {
            get => App.Settings.Prop.UseDiscordRichPresence;
            set
            {
                App.Settings.Prop.UseDiscordRichPresence = value;

                if (!value)
                {
                    DiscordActivityJoinEnabled = value;
                    DiscordAccountOnProfile = value;
                    OnPropertyChanged(nameof(DiscordActivityJoinEnabled));
                    OnPropertyChanged(nameof(DiscordAccountOnProfile));
                }
            }
        }
        public IReadOnlyList<DiscordRPCStatusDisplay> DiscordActivityStatusDisplayTypes => DiscordRPCStatusDisplayEx.Selections;

        public DiscordRPCStatusDisplay DiscordActivityStatusDisplayType
        {
            get => App.Settings.Prop.RichPresenceStatusDisplayType;
            set => App.Settings.Prop.RichPresenceStatusDisplayType = value;
        }

        public bool DiscordActivityJoinEnabled
        {
            get => !App.Settings.Prop.HideRPCButtons;
            set => App.Settings.Prop.HideRPCButtons = !value;
        }

        public bool DiscordAccountOnProfile
        {
            get => App.Settings.Prop.ShowAccountOnRichPresence;
            set => App.Settings.Prop.ShowAccountOnRichPresence = value;
        }

        public bool DisableAppPatchEnabled
        {
            get => App.Settings.Prop.UseDisableAppPatch;
            set => App.Settings.Prop.UseDisableAppPatch = value;
        }

        public ObservableCollection<CustomIntegration> CustomIntegrations
        {
            get => App.Settings.Prop.CustomIntegrations;
            set => App.Settings.Prop.CustomIntegrations = value;
        }

        public bool EnableWindowManipulation
        {
            get => App.Settings.Prop.EnableWindowManipulation;
            set
            {
                App.Settings.Prop.EnableWindowManipulation = value;

                if (!value)
                    EnableFakeBorderlessFullscreen = false;

                OnPropertyChanged(nameof(BorderlessFullscreenAllowed));
                OnPropertyChanged(nameof(EnableFakeBorderlessFullscreen));
            }
        }

        public bool EnableFakeBorderlessFullscreen
        {
            get => App.Settings.Prop.FakeBorderlessFullscreen;
            set => App.Settings.Prop.FakeBorderlessFullscreen = value;
        }

        public bool GameOverlayEnabled
        {
            get => App.Settings.Prop.EnableGameOverlay;
            set => App.Settings.Prop.EnableGameOverlay = value;
        }

        public byte GameOverlayOpacity
        {
            get => App.Settings.Prop.GameOverlayOpacity;
            set
            {
                App.Settings.Prop.GameOverlayOpacity = value;
                OnPropertyChanged(nameof(GameOverlayOpacity));
            }
        }

        public bool GameOverlayAutoHide
        {
            get => App.Settings.Prop.GameOverlayAutoHide;
            set => App.Settings.Prop.GameOverlayAutoHide = value;
        }

        public IReadOnlyCollection<GameOverlaySize> OverlaySizes { get; } =
            Enum.GetValues(typeof(GameOverlaySize)).Cast<GameOverlaySize>().ToList();

        public GameOverlaySize GameOverlaySize
        {
            get => App.Settings.Prop.GameOverlaySize;
            set
            {
                App.Settings.Prop.GameOverlaySize = value;
                OnPropertyChanged(nameof(GameOverlaySize));
            }
        }

        public int GameOverlayPingInterval
        {
            get => App.Settings.Prop.GameOverlayPingInterval;
            set
            {
                App.Settings.Prop.GameOverlayPingInterval = value;
                OnPropertyChanged(nameof(GameOverlayPingInterval));
            }
        }

        public bool GameOverlayShowClock
        {
            get => App.Settings.Prop.GameOverlayShowClock;
            set => App.Settings.Prop.GameOverlayShowClock = value;
        }

        public bool GameOverlaySuperMini
        {
            get => App.Settings.Prop.GameOverlaySuperMini;
            set => App.Settings.Prop.GameOverlaySuperMini = value;
        }

        public bool StreamerMode
        {
            get => App.Settings.Prop.StreamerMode;
            set => App.Settings.Prop.StreamerMode = value;
        }

        public ICommand OpenScreenshotsFolderCommand => new RelayCommand(OpenScreenshotsFolder);

        private void OpenScreenshotsFolder()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "wStrap");
            Directory.CreateDirectory(dir);
            Utilities.ShellExecute(dir);
        }

        public bool ShowInGameFps
        {
            get => App.FastFlags.GetValue("FFlagDebugDisplayFPS") == "True";
            set => App.FastFlags.SetValue("FFlagDebugDisplayFPS", value ? "True" : null);
        }

        public CustomIntegration? SelectedCustomIntegration { get; set; }
        public int SelectedCustomIntegrationIndex { get; set; }
        public bool IsCustomIntegrationSelected => SelectedCustomIntegration is not null;
    }
}
