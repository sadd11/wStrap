using DiscordRPC;
using System.Collections.ObjectModel;

namespace Bloxstrap.Models.Persistable
{
    public class Settings
    {
        // uh
        public bool AllowCookieAccess { get; set; } = false;

        // bloxstrap configuration
        public BootstrapperStyle BootstrapperStyle { get; set; } = BootstrapperStyle.FluentAeroDialog;
        public BootstrapperIcon BootstrapperIcon { get; set; } = BootstrapperIcon.IconBloxstrap;
        public string BootstrapperTitle { get; set; } = App.ProjectName;
        public string BootstrapperIconCustomLocation { get; set; } = "";
        public RobloxIcon RobloxIcon { get; set; } = RobloxIcon.IconDefault;
        public string RobloxTitle { get; set; } = "Roblox";
        public string RobloxIconCustomLocation { get; set; } = "";
        public Theme Theme { get; set; } = Theme.Default;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool DeveloperMode { get; set; } = false;
        // PLEASE DONT FORGET TO TURN THIS OFF !!!!
        public bool UseAcrylicBackground { get; set; } = false;
        public byte AcrylicBackgroundOpacity { get; set; } = 165;
        public bool ForceLocalData { get; set; } = false;
        public bool CheckForUpdates { get; set; } = true;
        public bool ConfirmLaunches { get; set; } = true;
        public bool EnableRobloxBackgroundApp { get; set; } = false;
        public RobloxTheme RobloxTheme { get; set; } = RobloxTheme.Default;
        public string Locale { get; set; } = "nil";
        public bool ForceRobloxLanguage { get; set; } = false;
        public bool UseFastFlagManager { get; set; } = true;
        public bool WPFSoftwareRender { get; set; } = false;
        public bool EnableAnalytics { get; set; } = false;
        public bool StaticDirectory { get; set; } = false;
        public string Channel { get; set; } = RobloxInterfaces.Deployment.DefaultChannel;
        public string RobloxDomain { get; set; } = RobloxInterfaces.Deployment.DefaultRobloxDomain;
        public ChannelChangeMode ChannelChangeMode { get; set; } = ChannelChangeMode.Automatic;
        public DistributorType DistributorType { get; set; } = DistributorType.Global;
        public string? SelectedCustomTheme { get; set; } = null;
        public bool BackgroundUpdatesEnabled { get; set; } = false;
        public bool DebugDisableVersionPackageCleanup { get; set; } = false;
        public bool EnableBetterMatchmaking { get; set; } = false;
        public bool EnableBetterMatchmakingRandomization { get; set; } = false;
        public WebEnvironment WebEnvironment { get; set; } = WebEnvironment.Production;

        // integration configuration
        public CleanerOptions CleanerOptions { get; set; } = CleanerOptions.TwoWeeks;
        // how do i automate this? -Naveandice
        public List<string> CleanerDirectories { get; set; } = new List<string> {
            "RobloxCache",
            "RobloxStudioCache",
            "RobloxLogs",
            "FishstrapLogs"
        };
        public bool EnableWindowManipulation { get; set; } = false;
        public bool FakeBorderlessFullscreen { get; set; } = false;
        public bool EnableActivityTracking { get; set; } = true;
        public bool EnableGameStatistics { get; set; } = true;
        public bool EnableGameOverlay { get; set; } = false;
        public byte GameOverlayOpacity { get; set; } = 200;
        public bool GameOverlayAutoHide { get; set; } = false;
        public GameOverlaySize GameOverlaySize { get; set; } = GameOverlaySize.Normal;
        public int GameOverlayPingInterval { get; set; } = 2;
        public bool GameOverlayShowClock { get; set; } = false;
        public bool GameOverlaySuperMini { get; set; } = false;
        public bool ScreenshotBufferOnly { get; set; } = false;

        // launch
        public bool LaunchHighPriority { get; set; } = false;

        // privacy
        public bool DeleteRobloxLogsAfterSession { get; set; } = false;

        // streamer
        public bool StreamerMode { get; set; } = false;

        // sound
        public bool PlayJoinSound { get; set; } = false;

        // appearance
        public string InterfaceAccent { get; set; } = "";
        public string InterfaceFont { get; set; } = "Rubik";
        public bool UseDiscordRichPresence { get; set; } = true;
        public DiscordRPCStatusDisplay RichPresenceStatusDisplayType { get; set; } = DiscordRPCStatusDisplay.Name;
        public bool HideRPCButtons { get; set; } = true;
        public bool ShowAccountOnRichPresence { get; set; } = false;
        public bool ShowServerDetails { get; set; } = false;
        public ObservableCollection<CustomIntegration> CustomIntegrations { get; set; } = new();

        // mod preset configuration
        public bool UseDisableAppPatch { get; set; } = false;
    }
}
