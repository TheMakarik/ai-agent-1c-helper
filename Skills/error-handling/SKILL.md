---
name: error-handling
description: Обработка ошибок в C#: конкретные исключения, не глотать, логировать и пробрасывать с сохранением стек-трейса, границы приложения, ThrownExceptions у ReactiveCommand, ArgumentNullException.ThrowIfNull. Используй при написании try/catch и обработке сбоев.
---

# Skill: error-handling

# Обработка ошибок

## Общие правила

- Ловить только конкретные исключения, которые реально обрабатываются.
- Не глотать исключения: не пустой `catch`, не `catch` без логирования.
- Логировать с исключением первым аргументом (см. скилл `logging`).
- Пробрасывать `throw;`, чтобы не терять стек-трейс.

```csharp
// Хорошо — конкретное исключение, лог и проброс
catch (IOException ex)
{
    Log.Logger.Error(ex, "Не удалось прочитать файл {Path}", path);
    throw;
}

// Плохо — проглотили ошибку
catch (Exception) { }

// Плохо — потеряли стек-трейс
catch (Exception ex) { throw ex; }
```

## Обёртывание

Оборачивай, сохраняя внутреннее исключение как `innerException`.

```csharp
// Хорошо
catch (IOException ex)
{
    throw new InvalidOperationException("Не удалось загрузить конфигурацию", ex);
}

// Плохо — потеряли причину
catch (IOException)
{
    throw new InvalidOperationException("Не удалось загрузить конфигурацию");
}
```

## Границы приложения

Ловля на границе допускается только в точках композиции и стартапа — `App.OnFrameworkInitializationCompleted`:

```csharp
try
{
    var host = await new Bootstrapper().LoadApplicationAsync();
    desktop.MainWindow = new MainWindow { DataContext = host.Services.GetRequiredService<MainViewModel>() };
}
catch (Exception ex)
{
    Log.Logger.Error(ex, "Error while loading application");
}
```

Внутри Core/Modules общий `catch (Exception)` на границах методов без необходимости не ставить.

## Ошибки ReactiveCommand

Ошибки команд ReactiveUI обрабатываются через `ThrownExceptions`, а не тонут молча.

```csharp
public MainViewModel(IGreetingProvider greetingProvider)
{
    LoadCommand.ThrownExceptions
        .Subscribe(ex => Log.Error(ex, "Команда завершилась ошибкой"));
}
```

- Обработчик `ThrownExceptions` обязателен для команд, которые могут упасть.
- Не «глотать» ошибку пустым `Subscribe()`.

## Валидация аргументов

Вход-аргументы проверяй сразу, до логики.

```csharp
// Хорошо
public GreetingProvider(IOptions<GreetingOptions> options)
{
    ArgumentNullException.ThrowIfNull(options);
    _options = options.Value;
}
```

## Запреты

- Не использовать исключения для управления потоком (проверяй условие заранее).
- Не `catch (Exception)` там, где можно поймать конкретное.
- Не глотать ошибки пустым `catch`/пустой подпиской.

## Чек-лист

- [ ] Ловятся конкретные исключения.
- [ ] Ошибки логируются и пробрасываются, стек-трейс не теряется.
- [ ] Обёртки сохраняют `innerException`.
- [ ] Общий `catch` только на границе (`App`/`Bootstrapper`).
- [ ] Ошибки ReactiveCommand обрабатываются через `ThrownExceptions`.
- [ ] Аргументы валидируются через `ArgumentNullException.ThrowIfNull`.
