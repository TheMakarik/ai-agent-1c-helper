---
name: code-style
description: C#-конвенции проекта: var по умолчанию, is null/is not null, ранний return, без лишних скобок, sealed, readonly, именование PascalCase/_camelCase, file-scoped namespaces, современный C#. Используй при написании и ревью C#-кода.
---

# Skill: code-style

# Стиль кода C#

## Файлы и пространства имён

- File-scoped namespaces: `namespace Ai1CAgent.Core.Common;`
- Порядок: `using` (System сверху), namespace, тип.
- Один публичный тип на файл; имя файла совпадает с типом.

## `var`

По умолчанию — `var`, когда тип очевиден из правой части.

Исключение — collection expressions: явный тип, чтобы был виден вид коллекции (см. скилл `collections`).

```csharp
// Хорошо
var provider = new GreetingProvider(options);

// Хорошо — явный тип для collection expression
List<string> items = [];
int[] numbers = [1, 2, 3];

// Плохо
var items = [];
GreetingProvider provider = new GreetingProvider(options);
```

## Проверки на null

Только `is null` / `is not null`.

```csharp
// Хорошо
if (options is null)
    throw new ArgumentNullException(nameof(options));

// Плохо
if (options == null) { ... }
```

## Скобки, ветвление, ранний выход

- Однострочные тела без лишних фигурных скобок.
- Ранний `return` вместо вложенных `if` (guard clauses).

```csharp
// Хорошо — ранний выход
if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
{
    base.OnFrameworkInitializationCompleted();
    return;
}

// Плохо — глубокая вложенность
if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
{
    // ...
}
else
{
    base.OnFrameworkInitializationCompleted();
}
```

## Модификаторы

- `sealed` для классов, от которых не наследуются.
- Явный `private` для полей.
- `readonly` для полей, неизменяемых после конструктора.

```csharp
// Хорошо
public sealed class GreetingProvider : IGreetingProvider
{
    private readonly GreetingOptions _options;
}
```

## Именование

- Типы, методы, свойства — `PascalCase`.
- Параметры, локальные переменные — `camelCase`.
- Private-поля — `_camelCase`.
- Интерфейсы — с префиксом `I`.
- Асинхронные методы — суффикс `Async`.

```csharp
// Хорошо
public interface IGreetingProvider { }
private readonly GreetingOptions _options;
public Task LoadApplicationAsync() => ...;
```

## Современный C#

- Primary constructors:

```csharp
// Хорошо
public sealed class GreetingProvider(IOptions<GreetingOptions> options) : IGreetingProvider
{
    public string GetGreeting() => options.Value.Message;
}
```

- Pattern matching:

```csharp
// Хорошо
if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
```

- Collection expressions `[...]` — с явным типом.

## Чек-лист

- [ ] `var` по умолчанию; явный тип для collection expressions.
- [ ] `is null`/`is not null`, не `== null`.
- [ ] Ранний `return`, без лишних фигурных скобок.
- [ ] `sealed`, явный `private`, `readonly`.
- [ ] Именование: `PascalCase`/`camelCase`/`_camelCase`, `I` у интерфейсов, `Async` у асинхронных.
- [ ] File-scoped namespaces, современный C#.
