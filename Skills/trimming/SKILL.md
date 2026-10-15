---
name: trimming
description: "Full trimming и AOT-совместимость .NET: PublishTrimmed/TrimMode=full, IsTrimmable/IsAotCompatible, аннотации DynamicallyAccessedMembers/RequiresUnreferencedCode/RequiresDynamicCode, source generators вместо рефлексии, запрет Reflection.Emit/dynamic/Expression.Compile, борьба с IL2xxx/IL3xxx. Используй при любом коде, рефлексии, сериализации, DI, опциях, логировании, XAML/Avalonia."
---

# Skill: trimming

# Разработка под full trimming

## Главное правило

Любой код пишется так, чтобы приложение собиралось и работало под **full trimming**, а значит — было **AOT-ready**. Триммер удаляет всё, до чего не дотягивается статический анализ. Если код использует рефлексию без аннотаций — он молча удалится и упадёт в рантайме.

**Предупреждения `IL2xxx` (trimming) и `IL3xxx` (AOT) — ошибки.** Не подавлять, а чинить. Подавление — только осознанное и с обоснованием.

## Настройки проектов

`Ai1CAgent.Avalonia` (точка публикации):

```xml
<PropertyGroup>
    <PublishTrimmed>true</PublishTrimmed>
    <TrimMode>full</TrimMode>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

`Ai1CAgent.Core` и `Ai1CAgent.Modules` (библиотеки):

```xml
<PropertyGroup>
    <IsAotCompatible>true</IsAotCompatible>
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
    <EnableAotAnalyzer>true</EnableAotAnalyzer>
</PropertyGroup>
```

- `IsAotCompatible=true` уже включает `IsTrimmable` и оба анализатора — не дублировать их вручную.
- Тестовый проект (`Ai1CAgent.Tests`) **не** триммится и не помечается `IsAotCompatible`.
- `Core` не тянет внешних зависимостей — держать его trim-clean проще всего, начинать с него.

## Что запрещено

Всё, что строит код или тянет типы по строкам/рефлексии без аннотаций:

```csharp
// Плохо — типы по строке, триммер их не видит
var type = Type.GetType("Ai1CAgent.Modules.Foo");
var instance = Activator.CreateInstance(type)!;
var method = type.GetMethod("Bar");
method!.Invoke(instance, null);

// Плохо — динамическая генерация кода
dynamic value = GetSomething();
Expression.Lambda<Func<int>>(body).Compile();
Assembly.Load("Ai1CAgent.Plugins");
typeof(List<>).MakeGenericType(concrete);
```

Запрещено: `Assembly.Load*`, `Type.GetType(string)`, `Activator.CreateInstance(Type)`, `MakeGenericType`/`MakeGenericMethod` без аннотаций, `dynamic`, `Expression.Compile()`, `Reflection.Emit`, `System.Text.Json` в reflection-режиме, reflection-based DI (Scrutor).

## Аннотации — когда рефлексия неизбежна

Рефлексия допускается, только если каждый её путь размечен:

```csharp
// Хорошо — сообщаем триммеру, какие члены нужны
private static object Create(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    => Activator.CreateInstance(type)!;

// Хорошо — помечаем метод как нетриммингобезопасный
[RequiresUnreferencedCode("Использует рефлексию по строковым именам свойств.")]
public static object MapByName(object source, string propertyName) { ... }

// Хорошо — AOT-небезопасный путь помечен явно
[RequiresDynamicCode("Компилирует выражение в рантайме.")]
public static Delegate Compile(string expression) { ... }
```

- `[DynamicallyAccessedMembers]` — сохранить нужные члены типа.
- `[RequiresUnreferencedCode]` — метод не trim-safe; вызывающий обязан это учитывать.
- `[RequiresDynamicCode]` — метод требует генерации кода (несовместим с AOT).
- `[UnconditionalSuppressMessage("Trimming", "IL2xxx", Justification = "...")]` — крайняя мера, только с реальным обоснованием. Не глушить «чтобы собралось».

## Сериализация

`System.Text.Json` через source generator, никакой reflection-based сериализации.

```csharp
// Хорошо — source-generated контекст
[JsonSerializable(typeof(GreetingOptions))]
internal sealed partial class AppJsonContext : JsonSerializerContext;

var options = new JsonSerializerOptions { TypeInfoResolver = AppJsonContext.Default };
var json = JsonSerializer.Serialize(greeting, AppJsonContext.Default.GreetingOptions);
```

## DI и конфигурация

- Регистрация только вручную в `Bootstrapper` (см. скилл `dependency-injection`). Scrutor и прочий рефлективный DI — запрещён.
- `AddOptions<T>().Bind(...)` и `IConfiguration.Get<T>()` используют рефлексию. Включить source generator биндинга конфигурации:

```xml
<PropertyGroup>
    <EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>
</PropertyGroup>
```

## Логирование (Serilog)

- `ReadFrom.Configuration` ищет sinks по сборкам через рефлексию — sink-сборки триммятся. Сохранять их через `TrimmerRootAssembly`:

```xml
<ItemGroup>
    <TrimmerRootAssembly Include="Serilog.Sinks.Console" />
</ItemGroup>
```

- Альтернатива — конфигурировать sinks в коде, а не строкой из `appsettings.json`.
- Шаблоны структурированных логов (`{Message}`) триммингу не мешают — можно.

## Avalonia и XAML

- Компилируемые биндинги обязательны: `x:DataType` на каждом `Window`/`UserControl` + `AvaloniaUseCompiledBindingsByDefault=true`. Reflection-биндинги по строкам удаляются.
- Конвертеры — как `MarkupExtension` (см. скилл `avalonia-converters`), это trim-safe.
- ReactiveUI: `RaiseAndSetIfChanged` безопасен (имя свойства через `CallerMemberName`). Expression-based API (`WhenAnyValue`, `ObserveProperty`, `WhenAny`) строят деревья выражений — **избегать** в тримм-коде или проверять, что нет `IL3050`.
- Регулярки — через `[GeneratedRegex]`, не `new Regex(...)`.

## Проверка перед коммитом

- [ ] `dotnet build -c Release` без предупреждений `IL2xxx`/`IL3xxx`.
- [ ] `dotnet publish -c Release` проходит чисто.
- [ ] **Запустить опубликованный бинарник** и пройти основные сценарии: сломанный триммингом код падает только в рантайме (`MissingMethodException`, `TypeInitializationException`).
- [ ] При переходе на AOT: `dotnet publish -c Release -p:PublishAot=true` без `IL3050`.

## Чек-лист

- [ ] `PublishTrimmed=true`, `TrimMode=full`, библиотеки `IsAotCompatible=true`.
- [ ] Нет `Assembly.Load*`, `Type.GetType(string)`, `dynamic`, `Expression.Compile()`, `Reflection.Emit`.
- [ ] Нет reflection-based DI и сериализации — source generators.
- [ ] Рефлексия (если есть) размечена `[DynamicallyAccessedMembers]`/`[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`.
- [ ] Подавления `IL2xxx` — только с `Justification`.
- [ ] Компилируемые биндинги Avalonia (`x:DataType`), без expression-based ReactiveUI.
- [ ] Serilog-sinks сохранены через `TrimmerRootAssembly` или заданы в коде.
- [ ] Опубликованное приложение запущено и проверено вручную.
