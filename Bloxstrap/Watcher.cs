using Bloxstrap.AppData;
using Bloxstrap.Integrations;
using Bloxstrap.UI.Elements.Overlay;

namespace Bloxstrap
{
    public class Watcher : IDisposable
    {
        private readonly InterProcessLock _lock = new("Watcher");

        private readonly WatcherData? _watcherData;
        
        private readonly NotifyIconWrapper? _notifyIcon;

        public readonly ActivityWatcher? ActivityWatcher;

        public readonly WindowManipulation? WindowManipulation;

        public readonly DiscordRichPresence? RichPresence;

        public StatisticsTracker? StatisticsTracker;

        public GameOverlayWindow? Overlay;

        public Watcher()
        {
            const string LOG_IDENT = "Watcher";


            if (!_lock.IsAcquired)
            {
                App.Logger.WriteLine(LOG_IDENT, "Watcher instance already exists");
                return;
            }

            string? watcherDataArg = App.LaunchSettings.WatcherFlag.Data;

            if (String.IsNullOrEmpty(watcherDataArg))
            {
#if DEBUG
                string path = new RobloxPlayerData().ExecutablePath;
                if (!File.Exists(path))
                    throw new ApplicationException("Roblox player is not been installed");

                using var gameClientProcess = Process.Start(path);

                while (gameClientProcess.MainWindowHandle == IntPtr.Zero)
                    Thread.Sleep(100);

                _watcherData = new() { ProcessId = gameClientProcess.Id, Handle = gameClientProcess.MainWindowHandle.ToInt64() };
#else
                throw new Exception("Watcher data not specified");
#endif
            }
            else
            {
                _watcherData = JsonSerializer.Deserialize<WatcherData>(Encoding.UTF8.GetString(Convert.FromBase64String(watcherDataArg)));
            }

            if (_watcherData is null)
                throw new Exception("Watcher data is invalid");

            if (App.Settings.Prop.EnableWindowManipulation && _watcherData.Handle != 0)
                WindowManipulation = new(_watcherData.Handle, _watcherData.ProcessId);

            if (App.Settings.Prop.EnableActivityTracking)
            {
                ActivityWatcher = new(_watcherData.LogFile);

                if (App.Settings.Prop.UseDisableAppPatch)
                {
                    ActivityWatcher.OnAppClose += delegate
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Received desktop app exit, closing Roblox");
                        using var process = Process.GetProcessById(_watcherData.ProcessId);
                        process.CloseMainWindow();
                    };
                }

                if (App.Settings.Prop.UseDiscordRichPresence && !App.State.Prop.WatcherRunning)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Running rpc");
                    RichPresence = new(ActivityWatcher);
                }

                if (App.Settings.Prop.EnableGameStatistics)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Running statistics tracker");
                    StatisticsTracker = new(ActivityWatcher);
                }
            }

            _notifyIcon = new(this);

            if (App.Settings.Prop.EnableGameOverlay)
            {
                App.Logger.WriteLine(LOG_IDENT, "Running game overlay");
                // visible from the start, fills in ping and session time on game join
                Overlay = new GameOverlayWindow(this);
                Overlay.Show();
            }
        }

        public void KillRobloxProcess() => CloseProcess(_watcherData!.ProcessId, true);

        /// <summary>
        /// The window handle of the watched Roblox process, captured at launch.
        /// </summary>
        public IntPtr RobloxWindowHandle => (IntPtr)(_watcherData?.Handle ?? 0);

        public void ShowAlert(string caption, string message, int duration) => _notifyIcon?.ShowAlert(caption, message, duration, null);

        /// <summary>
        /// Kills Roblox and relaunches the current game through the bootstrapper.
        /// </summary>
        public void RestartRoblox()
        {
            const string LOG_IDENT = "Watcher::RestartRoblox";

            if (ActivityWatcher is null)
                return;

            ActivityData? activity = ActivityWatcher.InGame ? ActivityWatcher.Data : ActivityWatcher.History.FirstOrDefault();

            if (activity is null || activity.PlaceId == 0)
                return;

            // relaunch through the bootstrapper so mods and flags are freshly applied
            string deeplink = $"roblox://experiences/start?placeId={activity.PlaceId}";

            if (activity.ServerType == ServerType.Private && !String.IsNullOrEmpty(activity.AccessCode))
                deeplink += $"&accessCode={activity.AccessCode}";

            App.Logger.WriteLine(LOG_IDENT, $"Restarting Roblox (placeId={activity.PlaceId})");

            KillRobloxProcess();

            // wait for Roblox to fully exit so the fresh launch doesn't get hit with the already-running prompt
            for (int i = 0; i < 50 && Utilities.IsRobloxRunning(); i++)
                Thread.Sleep(100);

            Process.Start(Paths.Process, $"-player \"{deeplink}\"");
        }

        public void CloseProcess(int pid, bool force = false)
        {
            const string LOG_IDENT = "Watcher::CloseProcess";

            try
            {
                using var process = Process.GetProcessById(pid);

                App.Logger.WriteLine(LOG_IDENT, $"Killing process '{process.ProcessName}' (pid={pid}, force={force})");

                if (process.HasExited)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"PID {pid} has already exited");
                    return;
                }

                if (force)
                    process.Kill();
                else
                    process.CloseMainWindow();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"PID {pid} could not be closed");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public async Task Run()
        {
            if (!_lock.IsAcquired || _watcherData is null)
                return;

            // notify when a newer Roblox version is available
            _ = Task.Run(async () =>
            {
                try
                {
                    string installedVersion = App.RobloxState.Prop.Player.VersionGuid;

                    if (!String.IsNullOrEmpty(installedVersion))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(
                            await App.HttpClient.GetStringAsync("https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer"));

                        string? latest = doc.RootElement.GetProperty("version").GetString();

                        if (!String.IsNullOrEmpty(latest) && latest != installedVersion)
                            App.Current.Dispatcher.Invoke(() =>
                                ShowAlert("wStrap", string.Format(Strings.Watcher_UpdateAvailable, latest), 8));
                    }
                }
                catch (Exception)
                {
                    // purely informational
                }
            });

            ActivityWatcher?.Start();
            WindowManipulation?.Start();

            while (Utilities.GetProcessesSafe().Any(x => x.Id == _watcherData.ProcessId))
                await Task.Delay(1000);

            // record the session if roblox was closed without a clean disconnect
            StatisticsTracker?.RecordPendingSession();

            AppStorageManager.Apply();

            if (_watcherData.AutoclosePids is not null)
            {
                foreach (int pid in _watcherData.AutoclosePids)
                    CloseProcess(pid);
            }

            if (App.LaunchSettings.TestModeFlag.Active)
                Process.Start(Paths.Process, "-settings -testmode");
        }

        public void Dispose()
        {
            App.Logger.WriteLine("Watcher::Dispose", "Disposing Watcher");

            _notifyIcon?.Dispose();
            RichPresence?.Dispose();

            App.State.Prop.WatcherRunning = false;

            GC.SuppressFinalize(this);
        }
    }
}
