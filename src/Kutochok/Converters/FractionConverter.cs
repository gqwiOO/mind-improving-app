using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace Kutochok.Converters;

/// <summary>
/// Перетворює частку 0..1 на зіркову ширину колонки. Стовпчик графіка
/// займає свою частку, а решту з'їдає порожня колонка поруч — так бар
/// лишається пропорційним за будь-якої ширини вікна.
/// </summary>
public sealed class FractionConverter : IValueConverter
{
    /// <summary>Колонка самого стовпчика.</summary>
    public static readonly FractionConverter Fill = new(false);

    /// <summary>Колонка-порожнеча праворуч.</summary>
    public static readonly FractionConverter Rest = new(true);

    private readonly bool _complement;

    private FractionConverter(bool complement) => _complement = complement;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var fraction = value switch
        {
            double d => d,
            float f => f,
            _ => 0d,
        };

        fraction = Math.Clamp(fraction, 0d, 1d);
        var share = _complement ? 1d - fraction : fraction;

        // Нуль зірок ламає розкладку, тому лишаємо крихту
        return new GridLength(Math.Max(share, 0.0001), GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
