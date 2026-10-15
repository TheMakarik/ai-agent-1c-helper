---
name: async
description: "async/await в C#: суффикс Async, CancellationToken в API, запрет async void (кроме обработчиков) и .Result/.Wait(), Task.WhenAll, ConfigureAwait. Используй при написании и правке асинхронного кода."
---

# Skill: async

# Асинхронность (async/await)

## Именование

Асинхронные методы — с суффиксом `Async`.

```csharp
// Хорошо
public Task LoadApplicationAsync() { ... }

// Плохо
public Task LoadApplication() { ... }
```

## CancellationToken

Публичный асинхронный API принимает `CancellationToken` и пробрасывает его дальше.

```csharp
// Хорошо
public async Task<string> LoadAsync(string path, CancellationToken cancellationToken)
{
    await using var stream = File.OpenRead(path);
    using var reader = new StreamReader(stream);
    return await reader.ReadToEndAsync(cancellationToken);
}
```

## `async void`

`async void` запрещён, кроме обработчиков событий и методов жизненного цикла Avalonia вроде `OnFrameworkInitializationCompleted`.

```csharp
// Хорошо — точка входа в UI-фреймворк
public override async void OnFrameworkInitializationCompleted() { ... }

// Плохо — потерянные исключения
public async void DoWork() { ... }
```

## Блокировка

Не блокировать асинхронный код через `.Result`/`.Wait()`/`.GetAwaiter().GetResult()` — это риск дедлока.

```csharp
// Хорошо
var host = await new Bootstrapper().LoadApplicationAsync();

// Плохо
var host = new Bootstrapper().LoadApplicationAsync().Result;
```

## `Task` vs `ValueTask`

- `Task`/`Task<T>` — по умолчанию.
- `ValueTask`/`ValueTask<T>` — только при частых вызовах, которые часто завершаются синхронно.

## Параллельность

Несколько независимых операций — `Task.WhenAll`, а не последовательные `await`.

```csharp
// Хорошо — параллельно
var results = await Task.WhenAll(
    LoadAsync(pathA, cancellationToken),
    LoadAsync(pathB, cancellationToken));

// Плохо — последовательно
var a = await LoadAsync(pathA, cancellationToken);
var b = await LoadAsync(pathB, cancellationToken);
```

## `ConfigureAwait`

Для библиотечного кода без необходимости возвращаться в UI-контекст — `ConfigureAwait(false)`. В UI-коде (`Avalonia`, ViewModel), где нужен UI-контекст, — без `ConfigureAwait`.

```csharp
// Хорошо — Modules без UI-контекста
public async Task<string> ReadAsync(CancellationToken cancellationToken)
    => await File.ReadAllTextAsync(Path, cancellationToken).ConfigureAwait(false);
```

## Проброс отмены

Не глотать `OperationCanceledException` там, где отмена ожидаема — дать ей пройти.

```csharp
catch (OperationCanceledException)
{
    throw;
}
```

## Чек-лист

- [ ] Асинхронные методы — с суффиксом `Async`.
- [ ] Публичный API принимает и пробрасывает `CancellationToken`.
- [ ] Нет `async void`, кроме обработчиков событий.
- [ ] Нет `.Result`/`.Wait()`.
- [ ] Параллельные операции — `Task.WhenAll`.
- [ ] `ConfigureAwait(false)` в библиотечном коде.
