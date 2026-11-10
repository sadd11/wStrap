using Bloxstrap.Models.Entities;

namespace Bloxstrap.Integrations
{
    /// <summary>
    /// Records local per-game playtime statistics from activity watcher events.
    /// </summary>
    public class StatisticsTracker
    {
        private readonly ActivityWatcher _activityWatcher;

        /// <summary>
        /// Live toggle, lets the tray menu turn recording on and off mid-session.
        /// </summary>
        public bool Enabled = true;

        public StatisticsTracker(ActivityWatcher activityWatcher)
        {
            _activityWatcher = activityWatcher;

            // by the time this fires, the ended session has already been moved to the front of the history
            _activityWatcher.OnGameLeave += (_, _) =>
            {
                if (Enabled)
                    RecordSession(_activityWatcher.History.FirstOrDefault());
            };
        }

        /// <summary>
        /// Records the in-progress session, used when Roblox exits without a clean disconnect.
        /// </summary>
        public void RecordPendingSession()
        {
            if (Enabled && _activityWatcher.InGame)
                RecordSession(_activityWatcher.Data);
        }

        public static void RecordSession(ActivityData? activity)
        {
            const string LOG_IDENT = "StatisticsTracker::RecordSession";

            if (activity is null || activity.PlaceId == 0 || activity.TimeJoined == default)
                return;

            DateTime timeLeft = activity.TimeLeft ?? DateTime.Now;

            if (timeLeft <= activity.TimeJoined)
                return;

            string universeId = activity.UniverseId.ToString();

            if (String.IsNullOrEmpty(universeId) || universeId == "0")
            {
                App.Logger.WriteLine(LOG_IDENT, "Skipping session with no universe id");
                return;
            }

            lock (App.Statistics.Prop)
            {
                if (!App.Statistics.Prop.Games.TryGetValue(universeId, out GameStatisticsEntry? entry))
                {
                    entry = new GameStatisticsEntry();
                    App.Statistics.Prop.Games[universeId] = entry;
                }

                long seconds = (long)(timeLeft - activity.TimeJoined).TotalSeconds;

                entry.Name = activity.UniverseDetails?.Data.Name ?? entry.Name;
                entry.PlaceId = activity.PlaceId;
                entry.TotalSeconds += seconds;
                entry.SessionCount += 1;
                entry.LastPlayed = timeLeft;

                // attribute the session to the day and hour it started
                string day = activity.TimeJoined.ToString("yyyy-MM-dd");
                string hour = activity.TimeJoined.Hour.ToString();

                App.Statistics.Prop.DailySeconds.TryGetValue(day, out long dailySeconds);
                App.Statistics.Prop.DailySeconds[day] = dailySeconds + seconds;

                App.Statistics.Prop.HourlySeconds.TryGetValue(hour, out long hourlySeconds);
                App.Statistics.Prop.HourlySeconds[hour] = hourlySeconds + seconds;

                if (seconds > App.Statistics.Prop.LongestSessionSec)
                    App.Statistics.Prop.LongestSessionSec = (int)seconds;

                UnlockAchievements(seconds);
            }

            App.Logger.WriteLine(LOG_IDENT, $"Recorded session for universe {universeId}");

            App.Statistics.Save();

            // fetch the game name for display, we may not have it yet
            _ = Task.Run(async () =>
            {
                try
                {
                    long id = activity.UniverseId;

                    if (UniverseDetails.LoadFromCache(id) is null)
                        await UniverseDetails.FetchSingle(id);

                    var details = UniverseDetails.LoadFromCache(id);

                    if (details is null)
                        return;

                    lock (App.Statistics.Prop)
                    {
                        if (App.Statistics.Prop.Games.TryGetValue(id.ToString(), out GameStatisticsEntry? entry) && entry.Name != details.Data.Name)
                        {
                            entry.Name = details.Data.Name;
                            App.Statistics.Save();
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            });
        }

        /// <summary>
        /// Consecutive days played, ending today or yesterday.
        /// </summary>
        public static int GetStreak()
        {
            int streak = 0;
            var day = DateTime.Now.Date;

            // a streak survives if yesterday was played but today hasn't started yet
            if (!HasPlaytime(day))
                day = day.AddDays(-1);

            while (HasPlaytime(day))
            {
                streak++;
                day = day.AddDays(-1);
            }

            return streak;
        }

        private static bool HasPlaytime(DateTime day) =>
            App.Statistics.Prop.DailySeconds.TryGetValue(day.ToString("yyyy-MM-dd"), out long seconds) && seconds > 0;

        private static void UnlockAchievements(long sessionSeconds)
        {
            var stats = App.Statistics.Prop;
            var unlocked = new List<string>();

            void Unlock(string id, bool condition)
            {
                if (condition && !stats.Achievements.Contains(id))
                {
                    stats.Achievements.Add(id);
                    unlocked.Add(id);
                }
            }

            long totalSeconds = stats.Games.Values.Sum(x => x.TotalSeconds);

            Unlock("first_session", stats.Games.Count >= 1);
            Unlock("collector_10", stats.Games.Count >= 10);
            Unlock("collector_50", stats.Games.Count >= 50);
            Unlock("hours_10", totalSeconds >= 36000);
            Unlock("hours_100", totalSeconds >= 360000);
            Unlock("marathon_2h", sessionSeconds >= 7200);
            Unlock("marathon_5h", sessionSeconds >= 18000);
            Unlock("streak_7", GetStreak() >= 7);
            Unlock("night_owl", DateTime.Now.Hour is >= 0 and <= 5);

            if (unlocked.Any())
            {
                App.Logger.WriteLine("StatisticsTracker", $"Achievements unlocked: {string.Join(", ", unlocked)}");
                App.Statistics.Save();
            }
        }
    }
}
