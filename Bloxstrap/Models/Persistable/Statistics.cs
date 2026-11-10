namespace Bloxstrap.Models.Persistable
{
    public class Statistics
    {
        /// <summary>
        /// Collected game statistics, keyed by universe id.
        /// </summary>
        public Dictionary<string, GameStatisticsEntry> Games { get; set; } = new();

        /// <summary>
        /// Total playtime per day, keyed by date in the yyyy-MM-dd format.
        /// </summary>
        public Dictionary<string, long> DailySeconds { get; set; } = new();

        /// <summary>
        /// Total playtime per hour of day, keyed by hour (0-23).
        /// </summary>
        public Dictionary<string, long> HourlySeconds { get; set; } = new();

        public int LongestSessionSec { get; set; } = 0;

        public long TotalLaunches { get; set; } = 0;

        public List<string> Achievements { get; set; } = new();
    }

    public class GameStatisticsEntry
    {
        public string Name { get; set; } = "";

        /// <summary>
        /// The last place id joined for this game, used for quick launching.
        /// </summary>
        public long PlaceId { get; set; } = 0;

        public long TotalSeconds { get; set; } = 0;

        public int SessionCount { get; set; } = 0;

        public DateTime? LastPlayed { get; set; }
    }
}
