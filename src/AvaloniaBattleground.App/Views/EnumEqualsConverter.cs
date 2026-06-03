using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace AvaloniaBattleground.App.Views;

public sealed class EnumEqualsConverter : IValueConverter
{
    public static readonly EnumEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
        {
            return false;
        }

        var expected = parameter is Enum enumParameter
            ? enumParameter
            : Enum.Parse(value.GetType(), parameter.ToString()!, ignoreCase: true);

        return value.Equals(expected);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
