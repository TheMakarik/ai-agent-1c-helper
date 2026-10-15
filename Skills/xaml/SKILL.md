---
name: xaml
description: Разработка XAML/AXAML в Avalonia: форматирование по XAML Styler, стили, Grid, behaviors, структура Views, конвертеры как MarkupExtension. Используй при верстке и правке UI.
---

# XAML разработка

## Структура UI-папок в ZaggyCode.Avalonia
```
Views/
├── Behaviors/          # Поведения для взаимодействия (TextEditorZoomBehavior, TerminalZoomBehavior)
├── Converters/         # Конвертеры как MarkupExtension
├── Controls/           # Кастомные контролы (MapView)
├── UserControls/       # Составные контролы
├── Styles/             # Стили и темы
│   ├── Colors.axaml    # Цветовые ресурсы
│   └── Controls.axaml  # Стили контролов
├── Dialogs/            # Диалоговые окна
├── Utils/              # UI-утилиты
└── TerminalEngine/     # Реализация xterm движка (VirtualTerminal.Avalonia)
```

## Форматирование по XAML Styler

### Атрибуты
- Каждый атрибут на новой строке
- Порядок: `x:Class`, `xmlns`, `x:DataType`, остальные атрибуты

```xml
<rxui:ReactiveWindow
    x:Class="ZaggyCode.Avalonia.Views.MainWindow"
    x:DataType="vm:MainWindowViewModel"
    xmlns="https://github.com/avaloniaui"
    xmlns:rxui="http://reactiveui.net"
    xmlns:vm="using:ZaggyCode.Avalonia.ViewModels"
    xmlns:converters="clr-namespace:ZaggyCode.Avalonia.Views.Converters"
    Background="{DynamicResource BackgroundBrush}"
    Foreground="{DynamicResource ForegroundBrush}"
    Title="Программирование с Загги">
```

### Grid с ColumnDefinitions и RowDefinitions
```xml
<Grid ColumnDefinitions="Auto,*,Auto" RowDefinitions="Auto,Auto,*">
```

### Элементы внутри Grid
```xml
<StackPanel
    Grid.Column="0"
    Grid.Row="0"
    Orientation="Horizontal"
    Spacing="12">
```

## Иконки (FluentIcons)

Стек иконок — `FluentIcons.Avalonia`, namespace `using:FluentIcons.Avalonia`.

```xml
xmlns:ic="using:FluentIcons.Avalonia"

<ic:FluentIcon Icon="Add" IconVariant="Regular" IconSize="Size20" />
<ic:SymbolIcon Symbol="FolderOpen" />
```

### Обязательные правила
- **Каждый элемент меню обязан содержать иконку** (`Menu`, `MenuItem`, `ContextMenu`, выпадающие пункты, тулбар-пункты). Только текста недостаточно.
- **Каждая кнопка обязана содержать иконку** (`Button`, `ToggleButton`, `RepeatButton` и т.п.):
  - обычные кнопки — иконка **вместе с текстом**;
  - маленькие (иконочные) кнопки размером ~32×32 — **только иконка, без текста**.

```xml
<!-- Хорошо — пункт меню с иконкой -->
<MenuItem Header="Открыть">
    <MenuItem.Icon>
        <ic:SymbolIcon Symbol="FolderOpen" />
    </MenuItem.Icon>
</MenuItem>

<!-- Хорошо — кнопка: иконка + текст -->
<Button Command="{Binding AddCommand}">
    <StackPanel Orientation="Horizontal" Spacing="8">
        <ic:FluentIcon Icon="Add" IconVariant="Regular" IconSize="Size20" />
        <TextBlock VerticalAlignment="Center" Text="Добавить" />
    </StackPanel>
</Button>

<!-- Хорошо — маленькая иконочная кнопка: только иконка -->
<Button Classes="icon-button" Width="32" Height="32" Padding="0">
    <ic:FluentIcon Icon="Play" IconVariant="Regular" IconSize="Size20" />
</Button>

<!-- Плохо — кнопка/пункт меню без иконки -->
<Button Content="Добавить" />
<MenuItem Header="Открыть" />
```

## Использование стилей
Выноси повторяющуюся логику в стили:

```xml
<!-- В ресурсах -->
<Style Selector="Button.icon-button">
    <Setter Property="Width" Value="32" />
    <Setter Property="Height" Value="32" />
    <Setter Property="Padding" Value="0" />
    <Setter Property="Background" Value="Transparent" />
</Style>

<Style Selector="Button.icon-button.window-control">
    <Setter Property="Width" Value="36" />
    <Setter Property="Height" Value="36" />
</Style>
```

### Использование
```xml
<Button Classes="icon-button run-button" Command="{Binding ExecuteCodeCommand}">
    <ic:FluentIcon Icon="Play" IconVariant="Regular" IconSize="Size20" />
</Button>
```

## Behaviors для сложной логики
```xml
<avaloniaEdit:TextEditor>
    <Interaction.Behaviors>
        <behaviors:TextEditorZoomBehavior
            MaxFontSize="{Binding MaxFontSize}"
            MinFontSize="{Binding MinFontSize}"
            UpdateFontSizeCommand="{Binding UpdateFontSizeCommand}"
            ZoomStep="1" />
    </Interaction.Behaviors>
</avaloniaEdit:TextEditor>
```

## Адаптивность через конвертеры
```xml
<Menu IsVisible="{Binding Bounds.Width, Converter={converters:WidthToVisibilityConverter}, ConverterParameter='>=900'}">
    <!-- Полное меню -->
</Menu>

<Menu IsVisible="{Binding Bounds.Width, Converter={converters:WidthToVisibilityConverter}, ConverterParameter='<900'}">
    <!-- Компактное меню -->
</Menu>
```

## Конвертеры как MarkupExtension
Всегда используй `MarkupExtension` для конвертеров:

```xml
<!-- Хорошо -->
<TextBlock IsVisible="{Binding ShowSidebar, Converter={converters:InverseBooleanConverter}}" />

<!-- Плохо — через ресурсы -->
<TextBlock IsVisible="{Binding ShowSidebar, Converter={StaticResource InverseBooleanConverter}}" />
```
