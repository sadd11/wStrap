using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Bloxstrap.Integrations
{
    /// <summary>
    /// Polls the Roblox process for memory, CPU and I/O stats,
    /// watches for a hung client (watchdog) and detects crashes.
    /// </summary>
    public class RobloxMonitor : IDisposable
    {
        private readonly int _processId;
        private readonly Watcher _watcher;
        private System.Threading.Timer? _timer;

        private TimeSpan _lastCpuTime = TimeSpan.MinValue;
        private long _lastIoBytes = -1;
        private DateTime _lastSample = DateTime.MinValue;

        private int _hungSeconds = 0;

        public double MemoryMb { get; private set; }
        public int CpuPercent { get; private set; }
        public double IoSpeedMbps { get; private set; }
        public string ClientVersion { get; private set; } = "";

        /// <summary>Fired every poll with fresh stats</summary>
        public event EventHandler? StatsUpdated;

        /// <summary>Fired once when the client is confirmed hung for 30+ seconds</summary>
        public event EventHandler? ClientHung;

        public RobloxMonitor(int processId, Watcher watcher)
        {
            _processId = processId;
            _watcher = watcher;
        }

        public void Start()
        {
            try
            {
                ClientVersion = Utilities.GetRobloxVersionStr(new AppData.RobloxPlayerData());
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("RobloxMonitor::Start", $"Version lookup failed: {ex.Message}");
            }

            _timer = new System.Threading.Timer(_ => Poll(), null, 1500, 2000);
        }

        private void Poll()
        {
            try
            {
                using var process = Process.GetProcessById(_processId);

                // memory
                MemoryMb = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 1);

                // cpu: delta of total processor time across all cores
                var cpuNow = process.TotalProcessorTime;
                if (_lastCpuTime != TimeSpan.MinValue)
                {
                    var cpuDelta = (cpuNow - _lastCpuTime).TotalMilliseconds;
                    var wallDelta = (DateTime.Now - _lastSample).TotalMilliseconds;
                    if (wallDelta > 0)
                        CpuPercent = Math.Clamp((int)Math.Round(cpuDelta / wallDelta / Environment.ProcessorCount * 100.0), 0, 100);
                }
                _lastCpuTime = cpuNow;

                // I/O: cumulative read+write transfer bytes (network + disk), delta per interval
                long ioNow = GetProcessIoBytes(_processId);
                if (_lastIoBytes >= 0 && _lastSample != DateTime.MinValue)
                {
                    double seconds = (DateTime.Now - _lastSample).TotalSeconds;
                    IoSpeedMbps = seconds > 0 ? Math.Round((ioNow - _lastIoBytes) / 1024.0 / 1024.0 / seconds, 2) : 0;
                    if (IoSpeedMbps < 0) IoSpeedMbps = 0;
                }
                _lastIoBytes = ioNow;
                _lastSample = DateTime.Now;

                StatsUpdated?.Invoke(this, EventArgs.Empty);

                // watchdog: hung client detection
                if (App.Settings.Prop.GameOverlayWatchdog)
                {
                    bool hung = IsProcessHung(_processId);
                    _hungSeconds = hung ? _hungSeconds + 2 : 0;

                    if (_hungSeconds >= 30)
                    {
                        _hungSeconds = 0;
                        App.Logger.WriteLine("RobloxMonitor", "Client hung for 30s - watchdog triggered");
                        ClientHung?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                // the process may have exited between checks
                App.Logger.WriteLine("RobloxMonitor::Poll", $"Poll skipped: {ex.Message}");
            }
        }

        private long GetProcessIoBytes(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                return GetProcessIoCountersInternal(process.Handle);
            }
            catch
            {
                return _lastIoBytes < 0 ? 0 : _lastIoBytes;
            }
        }

        private long GetProcessIoCountersInternal(IntPtr handle)
        {
            if (!GetProcessIoCounters(handle, out var counters))
                return 0;

            return (long)(counters.ReadTransferCount + counters.WriteTransferCount + counters.OtherTransferCount);
        }

        private bool IsProcessHung(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                var hwnd = process.MainWindowHandle;
                if (hwnd == IntPtr.Zero)
                    return false;

                return IsHungAppWindow(hwnd);
            }
            catch
            {
                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS ioCounters);

        [DllImport("user32.dll")]
        private static extern bool IsHungAppWindow(IntPtr hwnd);

        /// <summary>
        /// Kills the Roblox client.
        /// </summary>
        public void KillClient() => _watcher.KillRobloxProcess();

        /// <summary>
        /// Rejoins the last joined place (falls back to the current session data).
        /// </summary>
        public void RejoinLastServer()
        {
            const string LOG_IDENT = "RobloxMonitor::RejoinLastServer";

            var activity = _watcher.ActivityWatcher?.History.FirstOrDefault()
                ?? _watcher.ActivityWatcher?.Data;

            if (activity is null || activity.PlaceId == 0)
            {
                App.Logger.WriteLine(LOG_IDENT, "No place to rejoin");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"roblox://experiences/start?placeId={activity.PlaceId}",
                    UseShellExecute = true,
                });
                App.Logger.WriteLine(LOG_IDENT, $"Rejoining place {activity.PlaceId}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
