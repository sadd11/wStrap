using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui.Common.Interfaces;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class GamesViewModel : NotifyPropertyChangedViewModel, INavigationAware
    {
        public class GameEntry
        {
            public string Name { get; set; } = "";
            public ImageSource? Icon { get; set; }
            public string DetailsText { get; set; } = "";
            public long PlaceId { get; set; } = 0;
            public ICommand PlayCommand { get; set; } = new RelayCommand(() => { });
        }

        public class DailyBar
        {
            public string Label { get; set; } = "";
            public double BarHeight { get; set; } = 2;
            public string Tooltip { get; set; } = "";
        }

        public class AchievementBadge
        {
            public string Name { get; set; } = "";
            public bool Unlocked { get; set; }
        }

        public class TopGameEntry
        {
            public int Rank { get; set; }
            public string Name { get; set; } = "";
            public string TimeText { get; set; } = "";
            public double Share { get; set; }
        }

        public ObservableCollection<GameEntry> GamesList { get; private set; } = new();

        public ObservableCollection<TopGameEntry> TopGames { get; private set; } = new();

        public bool HasTopGames => TopGames.Count > 0;

        public List<DailyBar> HeatBars { get; private set; } = new();

        public List<AchievementBadge> Achievements { get; private set; } = new();

        public string StreakText { get; private set; } = "";

        public string RecordsText { get; private set; } = "";

        public string WeekText { get; private set; } = "";

        private int _chartModeIndex = 0;

        public int ChartModeIndex
        {
            get => _chartModeIndex;
            set
            {
                _chartModeIndex = value;
                OnPropertyChanged(nameof(ChartModeIndex));
                RebuildChart();
            }
        }

        private Statistics? _snapshot;

        public List<DailyBar> DailyChart { get; private set; } = new();

        public bool HasChartData => DailyChart.Any(x => x.BarHeight > 2);

        public string TotalPlaytimeText { get; private set; } = "";

        public bool HasGames => GamesList.Count > 0;

        public bool NoGames => GamesList.Count == 0;

        public bool StatisticsEnabled
        {
            get => App.Settings.Prop.EnableGameStatistics;
            set => App.Settings.Prop.EnableGameStatistics = value;
        }

        public ICommand ResetStatisticsCommand => new RelayCommand(ResetStatistics);

        public void OnNavigatedTo() => LoadData();

        public void OnNavigatedFrom() { }

        public static string FormatPlaytime(long seconds)
        {
            TimeSpan span = TimeSpan.FromSeconds(seconds);

            if (span.TotalHours >= 1)
                return String.Format(Strings.Menu_Games_PlaytimeFormat_HM, (int)span.TotalHours, span.Minutes);

            if (span.TotalMinutes >= 1)
                return String.Format(Strings.Menu_Games_PlaytimeFormat_M, span.Minutes);

            return String.Format(Strings.Menu_Games_PlaytimeFormat_S, span.Seconds);
        }

        private async void LoadData()
        {
            const string LOG_IDENT = "GamesViewModel::LoadData";

            // work off a snapshot so we aren't touching the live copy
            Statistics snapshot;

            try
            {
                snapshot = JsonSerializer.Deserialize<Statistics>(JsonSerializer.Serialize(App.Statistics.Prop)) ?? new();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                snapshot = new();
            }

            // fetch game details so we have names and icons
            List<long> missingIds = snapshot.Games
                .Where(x => x.Key != "0" && x.Value.PlaceId != 0)
                .Select(x => long.Parse(x.Key))
                .Where(x => UniverseDetails.LoadFromCache(x) is null)
                .ToList();

            if (missingIds.Any())
            {
                try
                {
                    await UniverseDetails.FetchBulk(String.Join(',', missingIds));
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }

            var pairs = snapshot.Games
                .Where(x => x.Value.PlaceId != 0)
                .OrderByDescending(x => x.Value.LastPlayed)
                .ToList();

            long totalSeconds = pairs.Sum(x => x.Value.TotalSeconds);
            int totalSessions = pairs.Sum(x => x.Value.SessionCount);

            // top games of all time, ranked by playtime
            TopGames.Clear();
            foreach (var pair in pairs.OrderByDescending(x => x.Value.TotalSeconds).Take(5))
            {
                var stats = pair.Value;
                var details = UniverseDetails.LoadFromCache(long.TryParse(pair.Key, out long universeId) ? universeId : 0);

                TopGames.Add(new TopGameEntry
                {
                    Rank = TopGames.Count + 1,
                    Name = !String.IsNullOrEmpty(stats.Name) ? stats.Name : (details?.Data.Name ?? $"Place {stats.PlaceId}"),
                    TimeText = FormatPlaytime(stats.TotalSeconds),
                    Share = totalSeconds > 0 ? Math.Clamp(stats.TotalSeconds * 100.0 / totalSeconds, 1, 100) : 0,
                });
            }

            OnPropertyChanged(nameof(TopGames));
            OnPropertyChanged(nameof(HasTopGames));

            TotalPlaytimeText = String.Format(
                Strings.Menu_Games_TotalPlaytime,
                FormatPlaytime(totalSeconds)
            ) + $" • " + String.Format(Strings.Menu_Games_SessionsCount, totalSessions);

            OnPropertyChanged(nameof(TotalPlaytimeText));

            var list = new ObservableCollection<GameEntry>();

            foreach (var pair in pairs)
            {
                var stats = pair.Value;
                var details = UniverseDetails.LoadFromCache(long.TryParse(pair.Key, out long universeId) ? universeId : 0);

                var entry = new GameEntry
                {
                    Name = !String.IsNullOrEmpty(stats.Name) ? stats.Name : (details?.Data.Name ?? $"Place {stats.PlaceId}"),
                    PlaceId = stats.PlaceId,
                    Icon = LoadIcon(details),
                    DetailsText = $"{FormatPlaytime(stats.TotalSeconds)} • " +
                        String.Format(Strings.Menu_Games_SessionsCount, stats.SessionCount) +
                        (stats.LastPlayed is not null ? $" • " + String.Format(Strings.Menu_Games_LastPlayed, stats.LastPlayed.Value.ToString("d")) : ""),
                    PlayCommand = new RelayCommand(() => LaunchGame(stats.PlaceId)),
                };

                list.Add(entry);
            }

            GamesList = list;

            _snapshot = snapshot;
            RebuildChart();
            BuildHeatmap(snapshot);
            BuildAchievements(snapshot);
            BuildSummary(snapshot);

            OnPropertyChanged(nameof(GamesList));
            OnPropertyChanged(nameof(DailyChart));
            OnPropertyChanged(nameof(HasChartData));
            OnPropertyChanged(nameof(HasGames));
            OnPropertyChanged(nameof(NoGames));
        }

        private void RebuildChart()
        {
            if (_snapshot is null)
                return;

            const double MaxBarHeight = 72;

            var bars = new List<DailyBar>();
            long max = 0;
            var buckets = new List<(string Label, string Key)>();

            if (_chartModeIndex == 1)
            {
                // last 8 weeks, keyed by the week's monday
                var monday = DateTime.Now.Date.AddDays(-(int)(DateTime.Now.Date.DayOfWeek - DayOfWeek.Monday) - 7 * 7);

                for (int i = 0; i < 8; i++)
                {
                    var start = monday.AddDays(i * 7);
                    var key = string.Join("|", Enumerable.Range(0, 7).Select(d => start.AddDays(d).ToString("yyyy-MM-dd")));
                    buckets.Add((start.ToString("dd.MM"), key));
                }
            }
            else if (_chartModeIndex == 2)
            {
                // last 6 months
                var month = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-5);

                for (int i = 0; i < 6; i++)
                {
                    var m = month.AddMonths(i);
                    var key = string.Join("|", Enumerable.Range(0, DateTime.DaysInMonth(m.Year, m.Month)).Select(d => m.AddDays(d).ToString("yyyy-MM-dd")));
                    buckets.Add((m.ToString("MM.yy"), key));
                }
            }
            else
            {
                // last 14 days
                for (int i = 13; i >= 0; i--)
                {
                    var day = DateTime.Now.Date.AddDays(-i);
                    buckets.Add((day.ToString("dd.MM"), day.ToString("yyyy-MM-dd")));
                }
            }

            var sums = new List<long>();

            foreach (var (_, key) in buckets)
            {
                long sum = key.Split('|').Sum(k => _snapshot.DailySeconds.TryGetValue(k, out long s) ? s : 0);
                sums.Add(sum);
                max = Math.Max(max, sum);
            }

            for (int i = 0; i < buckets.Count; i++)
            {
                long seconds = sums[i];

                double height = seconds <= 0 ? 2 : Math.Max(6, MaxBarHeight * seconds / Math.Max(max, 1));

                bars.Add(new DailyBar
                {
                    Label = buckets[i].Label,
                    BarHeight = height,
                    Tooltip = buckets[i].Label + ": " + FormatPlaytime(seconds),
                });
            }

            DailyChart = bars;
            OnPropertyChanged(nameof(DailyChart));
            OnPropertyChanged(nameof(HasChartData));
        }

        private void BuildHeatmap(Statistics snapshot)
        {
            const double MaxBarHeight = 60;

            long max = 0;
            var values = new long[24];

            for (int h = 0; h < 24; h++)
            {
                snapshot.HourlySeconds.TryGetValue(h.ToString(), out long s);
                values[h] = s;
                max = Math.Max(max, s);
            }

            var bars = new List<DailyBar>();

            for (int h = 0; h < 24; h++)
            {
                double height = values[h] <= 0 ? 2 : Math.Max(5, MaxBarHeight * values[h] / Math.Max(max, 1));

                bars.Add(new DailyBar
                {
                    Label = h.ToString("00"),
                    BarHeight = height,
                    Tooltip = h.ToString("00") + ":00 — " + FormatPlaytime(values[h]),
                });
            }

            HeatBars = bars;
            OnPropertyChanged(nameof(HeatBars));
        }

        private void BuildAchievements(Statistics snapshot)
        {
            var names = new Dictionary<string, string>
            {
                { "first_session", Strings.Menu_Games_Achievement_FirstSession },
                { "collector_10", Strings.Menu_Games_Achievement_Collector10 },
                { "collector_50", Strings.Menu_Games_Achievement_Collector50 },
                { "hours_10", Strings.Menu_Games_Achievement_Hours10 },
                { "hours_100", Strings.Menu_Games_Achievement_Hours100 },
                { "marathon_2h", Strings.Menu_Games_Achievement_Marathon2H },
                { "marathon_5h", Strings.Menu_Games_Achievement_Marathon5H },
                { "streak_7", Strings.Menu_Games_Achievement_Streak7 },
                { "night_owl", Strings.Menu_Games_Achievement_NightOwl },
            };

            Achievements = names.Select(x => new AchievementBadge
            {
                Name = x.Value,
                Unlocked = snapshot.Achievements.Contains(x.Key),
            }).ToList();

            OnPropertyChanged(nameof(Achievements));
        }

        private void BuildSummary(Statistics snapshot)
        {
            int streak = Bloxstrap.Integrations.StatisticsTracker.GetStreak();

            StreakText = streak >= 2 ? string.Format(Strings.Menu_Games_Streak, streak) : "";

            if (snapshot.LongestSessionSec > 0)
            {
                long totalSessions = snapshot.Games.Values.Sum(x => x.SessionCount);
                long totalSeconds = snapshot.Games.Values.Sum(x => x.TotalSeconds);
                long avg = totalSessions > 0 ? totalSeconds / totalSessions : 0;

                RecordsText = string.Format(Strings.Menu_Games_Records,
                    FormatPlaytime(snapshot.LongestSessionSec),
                    FormatPlaytime(avg));
            }
            else
            {
                RecordsText = "";
            }

            // this week vs last week, weeks start on monday
            var monday = DateTime.Now.Date.AddDays(-(int)(DateTime.Now.Date.DayOfWeek - DayOfWeek.Monday));
            long thisWeek = Enumerable.Range(0, 7).Sum(i => snapshot.DailySeconds.TryGetValue(monday.AddDays(i).ToString("yyyy-MM-dd"), out long s) ? s : 0);
            long lastWeek = Enumerable.Range(0, 7).Sum(i => snapshot.DailySeconds.TryGetValue(monday.AddDays(-7 + i).ToString("yyyy-MM-dd"), out long s) ? s : 0);

            if (lastWeek > 0 && thisWeek > 0)
            {
                int percent = (int)Math.Round((thisWeek - lastWeek) * 100.0 / lastWeek);
                WeekText = string.Format(Strings.Menu_Games_WeekComparison,
                    FormatPlaytime(thisWeek),
                    string.Format(Strings.Menu_Games_WeekPercent, percent.ToString("+#;-#;0")));
            }
            else if (thisWeek > 0)
            {
                WeekText = string.Format(Strings.Menu_Games_WeekComparison, FormatPlaytime(thisWeek), "");
            }
            else
            {
                WeekText = "";
            }

            OnPropertyChanged(nameof(StreakText));
            OnPropertyChanged(nameof(RecordsText));
            OnPropertyChanged(nameof(WeekText));
        }

        private void BuildDailyChart(Statistics snapshot)
        {
            const double MaxBarHeight = 72;

            var bars = new List<DailyBar>();
            long max = 0;

            var days = Enumerable.Range(0, 14)
                .Select(i => DateTime.Now.Date.AddDays(-13 + i))
                .ToList();

            foreach (var day in days)
            {
                snapshot.DailySeconds.TryGetValue(day.ToString("yyyy-MM-dd"), out long seconds);
                max = Math.Max(max, seconds);
            }

            foreach (var day in days)
            {
                snapshot.DailySeconds.TryGetValue(day.ToString("yyyy-MM-dd"), out long seconds);

                double height = seconds <= 0 ? 2 : Math.Max(6, MaxBarHeight * seconds / Math.Max(max, 1));

                bars.Add(new DailyBar
                {
                    Label = day.ToString("dd.MM"),
                    BarHeight = height,
                    Tooltip = $"{day:d MMMM}: {FormatPlaytime(seconds)}",
                });
            }

            DailyChart = bars;
        }

        private static ImageSource? LoadIcon(UniverseDetails? details)
        {
            const string LOG_IDENT = "GamesViewModel::LoadIcon";

            if (details?.Thumbnail.ImageUrl is null || String.IsNullOrEmpty(details.Thumbnail.ImageUrl))
                return null;

            try
            {
                byte[] bytes = App.HttpClient.GetByteArrayAsync(details.Thumbnail.ImageUrl).Result;

                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = new MemoryStream(bytes);
                image.EndInit();
                image.Freeze();

                return image;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return null;
            }
        }

        private void LaunchGame(long placeId)
        {
            const string LOG_IDENT = "GamesViewModel::LaunchGame";

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
                App.Logger.WriteException(LOG_IDENT, ex);

                Frontend.ShowMessageBox(
                    Strings.Menu_Games_FailedToLaunch,
                    System.Windows.MessageBoxImage.Warning
                );
            }
        }

        private void ResetStatistics()
        {
            MessageBoxResult result = Frontend.ShowMessageBox(
                Strings.Menu_Games_ResetStatisticsConfirm,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxButton.YesNo
            );

            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            App.Statistics.Prop.Games.Clear();
            App.Statistics.Save();

            LoadData();
        }
    }
}
