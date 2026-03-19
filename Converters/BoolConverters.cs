using System.Globalization;

namespace CVGenerator.Converters;

/// <summary>Inverse un booléen : true → false et vice-versa.</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

/// <summary>
/// MultiConverter : retourne true uniquement si TOUS les booléens fournis sont true.
/// Utilisé pour masquer le cadre de statut pendant le chargement.
/// </summary>
public sealed class AndBoolConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values.OfType<bool>().All(b => b);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
