using System.Globalization;
using System.Windows.Data;

namespace Bloxstrap.UI.Converters
{
    public class AchievementOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool unlocked && unlocked ? 1.0 : 0.35;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
