---
name: architecture
description: Слои Core/Modules/Avalonia, направление зависимостей (Avalonia → Modules → Core), где что живёт и композиция в Bootstrapper. Используй при проектировании структуры проектов, размещении типов и проверке границ слоёв.
---

# Skill: architecture

# Архитектура решения (Core / Modules / Avalonia)

## Слои

- **`Ai1CAgent.Core`** — абстракции: интерфейсы, модели, опции. Без внешних зависимостей и без ссылок на другие проекты решения.
  - `Core/Common` — интерфейсы вроде `IGreetingProvider`.
  - `Core/Options` — классы опций вроде `GreetingOptions`.
- **`Ai1CAgent.Modules`** — реализации абстракций из Core. Ссылается **только** на `Ai1CAgent.Core`.
  - `Modules/Greeting` — реализация `GreetingProvider`.
- **`Ai1CAgent.Avalonia`** — UI и композиция: `Program`, `App`, `Bootstrapper`, `ViewModels`, `Views`. Ссылается на `Modules` (и транзитивно на `Core`).

## Направление зависимостей

```
Avalonia  ->  Modules  ->  Core
```

- Зависимости идут только в одну сторону. Core ничего не знает про Modules и Avalonia.
- Реализации лежат в Modules, потребляются через интерфейсы из Core.
- Композиция (кто на что подписывается) — только в Avalonia.

```csharp
// Хорошо — Core: чистая абстракция
namespace Ai1CAgent.Core.Common;
public interface IGreetingProvider
{
    string GetGreeting();
}

// Хорошо — Modules: реализация, зависит только от Core
namespace Ai1CAgent.Modules.Greeting;
public sealed class GreetingProvider : IGreetingProvider { /* ... */ }

// Плохо — UI-тип (Avalonia) протёк в Core/Modules
public interface IWindowService { Window CreateWindow(); }
```

## Где что живёт

| Нужно | Проект / папка |
|-------|----------------|
| Интерфейс, модель, опция | `Ai1CAgent.Core` |
| Реализация интерфейса | `Ai1CAgent.Modules` |
| Регистрация в DI, хоста, ViewModel, View | `Ai1CAgent.Avalonia` |

## Запреты

- В Core/Modules **нет** `using Avalonia.*`, `Window`, `Control`, `IClassicDesktopStyleApplicationLifetime`.
- Core не ссылается на Modules/Avalonia; Modules не ссылается на Avalonia.
- Нельзя регистрировать сервисы или строить DI вне `Bootstrapper`.

## Композиция и DI

- Все регистрации — в `Bootstrapper`: `RegisterOptions` и `RegisterServices`.
- Подробности — в скилле `dependency-injection`.
- Связь ViewModel -> View через Interactions — в скилле `mvvm`.

## Чек-лист

- [ ] Зависимости только `Avalonia -> Modules -> Core`.
- [ ] Абстракция в Core, реализация в Modules.
- [ ] Никаких UI-типов в Core/Modules.
- [ ] Регистрации сервисов — в `Bootstrapper`.
