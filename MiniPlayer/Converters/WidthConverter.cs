using System.Globalization;
using System.Windows.Data;

namespace MiniPlayer.Converters
{
    // 由 lvFileList 的標題欄位使用，
    // 使其寬度可以占滿整個控制項
    public class WidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double actualWidth)
            {
                return actualWidth;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}