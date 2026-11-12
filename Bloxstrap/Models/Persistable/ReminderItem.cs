namespace Bloxstrap.Models.Persistable
{
    public class ReminderItem
    {
        /// <summary>Time of day in HH:mm format</summary>
        public string Time { get; set; } = "12:00";

        /// <summary>Reminder text</summary>
        public string Text { get; set; } = "";

        /// <summary>Last date this reminder was marked done (yyyy-MM-dd), empty = not done</summary>
        public string LastDone { get; set; } = "";
    }
}
