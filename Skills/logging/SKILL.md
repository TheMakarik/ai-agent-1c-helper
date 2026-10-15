---
name: logging
description: Логирование через Serilog из appsettings.json, ILogger<T> и Log.Logger, структурированные логи, уровни, запрет логирования секретов. Используй при добавлении логов и настройке логирования.
---

# Skill: logging

# Логирование (Serilog)

## Конфигурация

Логирование настраивается через Serilog из секции `Serilog` в `appsettings.json`. Используется только Console sink.

```json
"Serilog": {
  "Using": ["Serilog.Sinks.Console"],
  "MinimumLevel": {
    "Default": "Debug",
    "Override": { "Microsoft": "Debug" }
  },
  "WriteTo": [
    {
      "Name": "Console",
      "Args": {
        "outputTemplate": "[{Timestamp:HH:mm}] [{Level}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
      }
    }
  ]
}
```

`LoggerConfiguration` создаётся один раз в `Bootstrapper` из конфигурации:

```csharp
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Logging
    .ClearProviders()
    .AddSerilog(Log.Logger, dispose: true);
```

## Использование

- `ILogger<T>` — через DI в сервисах (`Microsoft.Extensions.Logging.ILogger<T>`).
- `Log.Logger` — в местах без DI (стартап, `App`, границы приложения).

```csharp
// Хорошо — через DI
public sealed class GreetingProvider(ILogger<GreetingProvider> logger)
{
    public string GetGreeting()
    {
        logger.LogInformation("Запрошено приветствие");
        return _options.Message;
    }
}

// Хорошо — на границе без DI
Log.Logger.Error(ex, "Error while loading application");
```

## Структурированные логи

Передавай данные параметрами шаблона, а не склейкой строк.

```csharp
// Хорошо — поля станут структурой
Log.Information("Загружено {Count} элементов за {Elapsed} ms", count, elapsed);

// Плохо — данные теряются в строке
Log.Information("Загружено " + count + " элементов");
```

## Уровни

- `Debug` — подробности разработки.
- `Information` — нормальные значимые события.
- `Warning` — неожиданное, но не сбой.
- `Error` — ошибка с исключением.
- `Fatal` — приложение не может продолжать работу.

## Исключения

Исключение передаётся первым аргументом, а не в шаблон.

```csharp
// Хорошо
Log.Logger.Error(ex, "Не удалось загрузить приложение");

// Плохо — стек-трейс теряется
Log.Logger.Error($"Не удалось загрузить приложение: {ex}");
```

## Запреты

- Не логировать секреты, токены, пароли и персональные данные.
- Не использовать `Console.WriteLine`/`Debug.WriteLine` для логирования приложения.
- Не склеивать данные в строку сообщения — только структурированные параметры.

## Чек-лист

- [ ] Serilog настроен через секцию `Serilog` в `appsettings.json`.
- [ ] `LoggerConfiguration` создаётся в `Bootstrapper` из конфигурации.
- [ ] В DI — `ILogger<T>`, на границе — `Log.Logger`.
- [ ] Логи структурированные, исключения — первым аргументом.
- [ ] В логи не попадают секреты и персональные данные.
