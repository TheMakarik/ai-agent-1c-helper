---
name: mvvm
description: MVVM с ReactiveUI в Avalonia: во ViewModel нельзя кидать View (не создавать окна, не вызывать ShowDialog). Используй Interactions и/или UI-сервисные абстракции для связи VM→View; RegisterHandler только в code-behind View.
---

# Skill: mvvm

# MVVM-паттерн с ReactiveUI и Avalonia (Interactions)

## Цель
Указать правило: **во ViewModel нельзя кидать View** (не создавать окна, не вызывать ShowDialog, не хранить ссылки на Window/Control). Коммуникация VM->View происходит через **Interactions** и (опционально) сервисные абстракции.

## Основные правила
- ViewModel не должна знать про конкретные классы View (`Window`, `UserControl`, `Dialog`). Только абстрактные запросы.
- Для VM->View используем `Interaction<TInput, TOutput>` из ReactiveUI.
- При необходимости открывать диалоги — инкапсулируем в **UI-сервис**-абстракции.
- Не делать `new Window()` в VM, не дергать `ShowDialog()` в VM.
- ViewModel должна быть тестируемой без UI.
- Ничего не додумывать — если данных не хватает, см. skill `clarifying-questions`.

## Interaction
```csharp
// В ViewModel
public Interaction<string, bool> ConfirmInteraction { get; } = new();

// В команде/методе VM
var result = await ConfirmInteraction.Handle("Удалить файл?");
if (result) { ... }

// В код-бихайнде View/Window
viewModel.ConfirmInteraction.RegisterHandler(async context =>
{
    var dialog = new ConfirmWindow { DataContext = context.Input };
    var ok = await dialog.ShowDialog<bool>(this);
    context.SetOutput(ok);
});
```
- `RegisterHandler` **только во View** (код-бихайнде `.axaml.cs`).
- Нельзя регистрировать handler из VM.
- Отписка при выгрузке (обычно не требуется в пределах жизни окна; при необходимости Dispose).

## Когда использовать Interaction
- Запросить информацию от UI (выбрать файл/путь, получить текст, принять решение).
- Показать окно/диалог и получить результат.
- Выполнить простой View-действие, который требует UI-контекста (фокус, прокрутка, play-animation) — если без View напрямую не сделать.
- UI-эффекты/тосты, модалки, копирование в буфер через абстракцию.

## Когда использовать сервисную абстракцию
```csharp
public interface IClipboardService
{
    Task SetTextAsync(string text);
}

public class AvaloniaClipboardService : IClipboardService
{
    public Task SetTextAsync(string text)
        => TopLevel.GetTopLevel(App.Current?.ApplicationLifetime...)?.Clipboard?.SetTextAsync(text) ?? Task.CompletedTask;
}

public class MyViewModel(IClipboardService clipboard)
{
    [ReactiveCommand]
    private async Task CopyAsync() => await clipboard.SetTextAsync("value");
}
```
- Для глобальных UI-действий (буфер обмена, навигация, выбор файлов через системный диалог, открытие URL).
- Регистрация сервиса в DI (ручная, без Scrutor).

## Запрещено
- Передавать `Window`, `Control`, `IClassicDesktopStyleApplicationLifetime` в конструктор/свойства VM.
- Создавать View из VM.
- Вызывать `ShowDialog`/`Show`/`Close` из VM.
- Хранить `DataContext` в связке "VM знает про View".

## Правильный путь (summary)
1. ViewModel объявляет `Interaction<TIn,TOut>` для VM->View запроса.
2. View (code-behind) регистрирует handler и реализует логику отображения/диалога.
3. Для системных UI-возможностей — отдельный `IXxxService` в DI.

## Как выбрать
- Нужен результат от пользователя через диалог → Interaction.
- Нужно просто выполнить системное UI-действие без сложного диалога → сервис.
- Нужно поменять состояние View без запроса → лучше через Binding/Styles/Behaviors; Interaction только при императивном вызове из VM.