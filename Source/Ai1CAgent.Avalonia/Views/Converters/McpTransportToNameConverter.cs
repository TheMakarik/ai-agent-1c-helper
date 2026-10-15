using System;
using System.Globalization;
using Ai1CAgent.Core.Models;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace Ai1CAgent.Avalonia.Views.Converters;

public sealed class McpTransportToNameConverter : MarkupExtension, IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not McpTransportType transport)
            return AvaloniaProperty.UnsetValue;

        return transport switch
        {
            McpTransportType.Http => "HTTP (адрес сервера)",
            McpTransportType.Stdio => "stdio (запуск процесса)",
            _ => AvaloniaProperty.UnsetValue,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
