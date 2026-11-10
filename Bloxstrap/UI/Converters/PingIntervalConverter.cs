using System.Globalization;
using System.Windows.Data;

namespace Bloxstrap.UI.Converters
{
    public class PingIntervalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is int seconds ? seconds switch { 1 => 0, 2 => 1, _ => 2 } : 1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is int index ? index switch { 0 => 1, 1 => 2, _ => 5 } : 2;
        }
    }
}
