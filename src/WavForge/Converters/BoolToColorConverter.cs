using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WavForge.Converters;

internal sealed class BoolToColorConverter : IValueConverter
{
    public IBrush TrueBrush { get; set; } = Brushes.IndianRed;
    public IBrush FalseBrush { get; set; } = Brushes.LightGray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool flag)
        {
            return flag ? TrueBrush : FalseBrush;
        }

        return FalseBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
