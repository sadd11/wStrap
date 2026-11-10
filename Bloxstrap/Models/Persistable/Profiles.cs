namespace Bloxstrap.Models.Persistable
{
    public class Profiles
    {
        /// <summary>
        /// Saved settings profiles, keyed by profile name.
        /// </summary>
        public Dictionary<string, SettingsProfile> Items { get; set; } = new();
    }

    public class SettingsProfile
    {
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Theme Theme { get; set; } = Theme.Default;

        public Dictionary<string, object> FastFlags { get; set; } = new();
    }
}
