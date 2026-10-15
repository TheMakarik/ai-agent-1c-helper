using System;
using System.Globalization;
using Ai1CAgent.Avalonia.ViewModels;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using FluentIcons.Common;

namespace Ai1CAgent.Avalonia.Views.Converters;

public sealed class AppSectionToIconConverter : MarkupExtension, IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not AppSection section)
            return AvaloniaProperty.UnsetValue;

        return section switch
        {
            AppSection.Chat => Icon.Chat,
            AppSection.Projects => Icon.Folder,
            AppSection.Documentation => Icon.DocumentText,
            AppSection.Changes => Icon.History,
            AppSection.Settings => Icon.Settings,
            _ => AvaloniaProperty.UnsetValue,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
