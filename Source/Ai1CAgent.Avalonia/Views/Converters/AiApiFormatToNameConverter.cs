using System;
using System.Globalization;
using Ai1CAgent.Core.Models;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace Ai1CAgent.Avalonia.Views.Converters;

public sealed class AiApiFormatToNameConverter : MarkupExtension, IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not AiApiFormat format)
            return AvaloniaProperty.UnsetValue;

        return format switch
        {
            AiApiFormat.OpenAi => "OpenAI",
            AiApiFormat.Anthropic => "Anthropic",
            _ => AvaloniaProperty.UnsetValue,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
