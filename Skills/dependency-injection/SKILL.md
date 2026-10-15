---
name: dependency-injection
description: Ручная регистрация в DI без Scrutor (AddSingleton/AddTransient), регистрация ViewModel и опций через AddOptions<T>().Bind, lifetime, конструкторная инъекция, где регистрировать (Bootstrapper). Используй при добавлении сервисов в DI.
---

# Skill: dependency-injection

# Dependency Injection (ручная регистрация)

## Где регистрировать

Все регистрации — в `Bootstrapper` (`Ai1CAgent.Avalonia`):

- `RegisterOptions(builder)` — опции через `AddOptions<T>().Bind(...)`.
- `RegisterServices(services)` — сервисы и ViewModel.

Никаких других мест для `services.Add...` в проекте нет.

```csharp
private static void RegisterOptions(HostApplicationBuilder builder)
{
    builder.Services
        .AddOptions<GreetingOptions>()
        .Bind(builder.Configuration.GetSection(nameof(GreetingOptions)));
}

private static void RegisterServices(IServiceCollection services)
{
    services.AddSingleton<IGreetingProvider, GreetingProvider>();
    services.AddSingleton<MainViewModel>();
}
```

## Без Scrutor

Регистрация только вручную, без сканирования сборок и без autowiring-атрибутов.

```csharp
// Хорошо — явно и читаемо
services.AddSingleton<IGreetingProvider, GreetingProvider>();

// Плохо — Scrutor / сканирование сборок
// services.Scan(...);
```

## Lifetime

- **Singleton** — сервисы без состояния или с общим состоянием на всё приложение (провайдеры, VM главного окна).
- **Transient** — дешёвые, короткоживущие объекты без разделяемого состояния.
- **Scoped** — не используется: десктопное приложение без запросов/scope.

```csharp
services.AddSingleton<IGreetingProvider, GreetingProvider>();
services.AddSingleton<MainViewModel>();
```

Не регистрируй Singleton, зависящий от Transient, если это даёт «захват» зависимости на всё время жизни.

## Построение хоста и получение сервисов

Хост строится один раз в `Bootstrapper`:

```csharp
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = Environment.GetCommandLineArgs(),
    ContentRootPath = AppContext.BaseDirectory,
});

// ... регистрации ...

var host = builder.Build();
await host.StartAsync();
return host;
```

Сервисы достаются из `host.Services` только в точке композиции (UI/стартап):

```csharp
desktop.MainWindow = new MainWindow
{
    DataContext = host.Services.GetRequiredService<MainViewModel>(),
};
```

## Конструкторная инъекция

Зависимости принимаются через конструктор, а не запрашиваются из контейнера.

```csharp
// Хорошо
public MainViewModel(IGreetingProvider greetingProvider)
{
    _greeting = greetingProvider.GetGreeting();
}

// Плохо — service locator внутри бизнес-кода
public MainViewModel(IServiceProvider provider)
{
    _greeting = provider.GetRequiredService<IGreetingProvider>().GetGreeting();
}
```

## Запреты

- Не использовать `IServiceProvider`/`GetService`/`GetRequiredService` внутри бизнес-логики, в Modules и Core.
- Не тянуть зависимости из контейнера в методах — только в конструкторе.
- Не регистрировать сервисы вне `Bootstrapper`.

## Чек-лист

- [ ] Регистрации только в `RegisterOptions`/`RegisterServices`.
- [ ] Ручное `AddSingleton`/`AddTransient`, без Scrutor.
- [ ] Опции через `AddOptions<T>().Bind(...)`.
- [ ] Зависимости — в конструкторе, без service locator.
