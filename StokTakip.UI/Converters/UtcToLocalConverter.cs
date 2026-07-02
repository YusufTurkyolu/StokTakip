using System.Globalization;
using System.Windows.Data;

namespace StokTakip.UI.Converters;

/// <summary>
/// UTC saklanan tarihleri ekranda yerel saate çevirir.
/// Excel aktarımı .ToLocalTime() kullanıyordu; rapor DataGrid'i ise ham UTC
/// gösteriyordu — bu converter ile iki yer tutarlı hale gelir.
/// </summary>
public class UtcToLocalConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime dt ? dt.ToLocalTime() : value;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
 