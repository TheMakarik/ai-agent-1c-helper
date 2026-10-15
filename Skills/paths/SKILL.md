---
name: paths
description: Правила работы с путями в C#: всегда использовать Path.Join вместо Path.Combine для склейки сегментов пути (кроссплатформенно, безопасно с rooted-сегментами). Используй при формировании путей, работе с файловой системой и опциями. Применяется во всех проектах и слоях.
---

# Пути: Path.Join вместо Path.Combine

## Правило
Во **всех** проектах и слоях (`Core`, `Modules`, `Avalonia`, `Tests`) для склейки сегментов пути используй `Path.Join`. `Path.Combine` — не использовать.

```csharp
// Хорошо
var dataPath = Path.Join(configDirectory, "data.json");
var libPath = Path.Join(root, "languages", "python", "Lib");

// Плохо
var dataPath = Path.Combine(configDirectory, "data.json");
```

## Почему не Path.Combine
- Перегрузки принимают 2–4 аргумента; для N сегментов нужна вложенность.
- Если любой последующий сегмент укоренён (`/abs` или `C:\`), `Path.Combine` **отбрасывает** предыдущие — частый источник трудноуловимых ошибок.
- `Path.Join` предсказуемо соединяет сегменты разделителем и кроссплатформен.

## Детали
- `Path.Join` не нормализует `.`/`..`. Нужна нормализация — делай её явно: `Path.GetFullPath(Path.Join(...))`.
- Не собирай пути конкатенацией строк со слэшами — только `Path.Join`.
- Пути из `appsettings.json` раскрываются через `Environment.ExpandEnvironmentVariables` в сеттерах опций (см. скилл `options`).
- В тестах пути формируй так же — через `Path.Join` (см. скилл `testing`).

## Чек-лист
- [ ] Нет `Path.Combine` — только `Path.Join`.
- [ ] Нет ручной конкатенации путей со слэшами.
- [ ] Нормализация (если нужна) — через `Path.GetFullPath`.
