---
name: json-sourcegen
description: "System.Text.Json Source Generators: JsonSerializerContext + [JsonSourceGenerationOptions], [JsonStringEnumConverter<T>] для enum, запрет reflection-based JsonSerializer в AOT/trimming-коде, передача контекста в Serialize/Deserialize, миграция схемы (новые поля необязательные с дефолтами). Используй при любой работе с JSON: DTO, опции, файлы, сетевые контракты."
---

# Skill: json-sourcegen

# System.Text.Json Source Generators

## Главное правило

Любой JSON-код пишется **только через source-generated контекст**. Reflection-based `JsonSerializer` (без контекста) в проектах, затрагивающих публикацию с AOT/Trimming, запрещён — он даёт `IL2026`/`IL3050` и ломается в рантайме (см. скилл `trimming`).

## Минимальный контекст

```csharp
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(IReadOnlyList<LogEntry>))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
```

- Один контекст на модуль/слой; именовать по типу домена (`AppJsonContext`, `OptionsJsonContext`).
- Все сериализуемые типы — через `[JsonSerializable(...)]`, включая коллекции и корневые типы.
- Опции задавать через `[JsonSourceGenerationOptions]`, не через `JsonSerializerOptions` в рантайме.

## Сериализация / десериализация

Всегда передавать контекст (или сгенерированный `JsonTypeInfo`):

```csharp
// Хорошо
var json = JsonSerializer.Serialize(config, AppJsonContext.Default.AppConfig);
var config = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppConfig);

// Хорошо — переиспользование опций через контекст
var options = new JsonSerializerOptions { TypeInfoResolver = AppJsonContext.Default };

// Плохо — reflection-based путь
var json = JsonSerializer.Serialize(config);
var config = JsonSerializer.Deserialize<AppConfig>(json);
```

- Перегрузки без `JsonTypeInfo`/`JsonSerializerContext` — не использовать в коде, который попадает в публикацию.
- Для `JsonSerializerOptions` задавать `TypeInfoResolver = <Context>.Default`.
- Дженерики: `JsonSerializer.Serialize(list, AppJsonContext.Default.ListLogEntry)` — имя свойства генератора = имя типа с суффиксом.

## Enum

Его свойство должно сериализоваться строкой (в API/файлах) — конвертер указывать явно, типобезопасно:

```csharp
[JsonSerializable(typeof(RunState))]
internal sealed partial class AppJsonContext : JsonSerializerContext;

// на уровне контекста (все enum в нём):
[JsonSourceGenerationOptions(Converters = [typeof(JsonStringEnumConverter<RunState>)])]
// или точечно на свойстве/типе:
public sealed class RunInfo
{
    [JsonConverter(typeof(JsonStringEnumConverter<RunState>))]
    public RunState State { get; set; }
}
```

- Только `JsonStringEnumConverter<T>` с явным `T` — не бездженерный `JsonStringEnumConverter` (рефлексия).
- Значение вне enum при десериализации падает — новые значения enum добавлять осознанно (см. миграцию ниже).

## Коллекции и типобезопасность

- Все используемые коллекции регистрировать в контексте: `IReadOnlyList<T>`, `Dictionary<string, T>`, `T[]` — каждая отдельным `[JsonSerializable]`.
- В DTO — интерфейсные типы (`IReadOnlyList<T>`), реализацию менять без правки вызывающего кода.
- Не смешивать: один и тот же тип сериализовать то через контекст, то без — всегда через контекст.
- `JsonElement`/`JsonDocument` для «неизвестной» части схемы — допустимы, они trim-safe.

## Миграция схемы

JSON может приходить от старых версий — поля обязаны быть обратно совместимыми:

- Новое поле — **необязательное**, со значением по умолчанию в модели:

```csharp
public sealed record AppConfig
{
    public string Host { get; init; } = "localhost";
    public int TimeoutSeconds { get; init; } = 30;   // новое поле — с дефолтом
    public List<string>? Tags { get; init; }         // или null
}
```

- `[JsonPropertyName]`/`[JsonIgnore]` — для переименования без разрыва старых файлов; старое имя держать через `JsonExtensionData` или alias-свойство, если контракт публичный.
- Нельзя переименовывать/удалять поля публичной схемы без волны миграции; удалённые поля — помечать `[JsonIgnore]` на чтение, чтобы старый JSON не ронял десериализацию.
- Unknown-поля в JSON генератор по умолчанию игнорирует — это желаемое поведение, не отключать `UnmappedMemberHandling`.

## Чек-лист

- [ ] Есть `JsonSerializerContext` с `[JsonSourceGenerationOptions]` и `[JsonSerializable]` на каждый тип.
- [ ] `Serialize`/`Deserialize` вызываются только с контекстом/`JsonTypeInfo`.
- [ ] Нет reflection-перегрузок (`JsonSerializer.Serialize(value)` без контекста) в коде публикации.
- [ ] Enum — через `JsonStringEnumConverter<T>` (явный `T`).
- [ ] Коллекции в DTO зарегистрированы в контексте.
- [ ] Новые поля схемы необязательные и с дефолтами; старый JSON десериализуется.
- [ ] Сборка без `IL2026`/`IL3050` (см. скилл `trimming`).
