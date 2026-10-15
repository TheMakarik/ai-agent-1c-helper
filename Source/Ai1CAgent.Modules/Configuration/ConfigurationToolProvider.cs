using Ai1CAgent.Modules.Configuration.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Ai1CAgent.Modules.Configuration;

public sealed class ConfigurationToolProvider(ILogger<ConfigurationToolProvider> logger) : IConfigurationToolProvider
{
    private const string FolderHint =
        "Папка — выгрузка конфигурации 1С (Конфигуратор → «Выгрузить конфигурацию в файлы»): " +
        "Configuration.xml — корень; метаданные объектов в <Вид>/<Имя>.xml (Catalogs, Documents, CommonModules, " +
        "AccumulationRegisters, InformationRegisters, DataProcessors, Reports и т. д.); " +
        "модули — <Вид>/<Имя>/Ext/ObjectModule.bsl, ManagerModule.bsl, Module.bsl, " +
        "формы — <Вид>/<Имя>/Forms/<Форма>/Ext/Form/Module.bsl. Пути относительные, разделитель «/».";

    private const string ListSchema = """
        {
          "type": "object",
          "properties": {
            "path": { "type": "string", "description": "Папка относительно корня выгрузки. Пусто — корень." }
          }
        }
        """;

    private const string ReadSchema = """
        {
          "type": "object",
          "properties": {
            "path": { "type": "string", "description": "Файл относительно корня выгрузки, например Catalogs/Номенклатура/Ext/ObjectModule.bsl" },
            "offset": { "type": "integer", "description": "Номер первой строки (с 1). По умолчанию 1." },
            "limit": { "type": "integer", "description": "Сколько строк прочитать, до 2000. По умолчанию 400." }
          },
          "required": ["path"]
        }
        """;

    private const string SearchSchema = """
        {
          "type": "object",
          "properties": {
            "query": { "type": "string", "description": "Искомый текст, без учёта регистра." },
            "file_pattern": { "type": "string", "description": "Маска файлов, например *.bsl или *.xml. По умолчанию все файлы." },
            "path": { "type": "string", "description": "Папка поиска относительно корня выгрузки. Пусто — вся выгрузка." }
          },
          "required": ["query"]
        }
        """;

    public bool IsConfigurationDump(string path) => ConfigurationFolder.IsConfigurationDump(path);

    public IReadOnlyList<AITool> GetTools(string configurationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);

        var folder = new ConfigurationFolder(configurationPath);

        return
        [
            new ConfigurationFunction(
                "list_files",
                "Показать содержимое папки в выгрузке конфигурации 1С: подпапки (с «/») и файлы с размером. " + FolderHint,
                ListSchema,
                arguments => Log("list_files", () => folder.List(ConfigurationFunction.GetString(arguments, "path")))),
            new ConfigurationFunction(
                "read_file",
                "Прочитать файл из выгрузки конфигурации 1С (модуль .bsl или метаданные .xml) с номерами строк. " + FolderHint,
                ReadSchema,
                arguments => Log("read_file", () => folder.Read(
                    ConfigurationFunction.GetString(arguments, "path") ?? string.Empty,
                    ConfigurationFunction.GetInt(arguments, "offset"),
                    ConfigurationFunction.GetInt(arguments, "limit")))),
            new ConfigurationFunction(
                "search_files",
                "Найти текст в файлах выгрузки конфигурации 1С: возвращает путь, номер строки и строку. " + FolderHint,
                SearchSchema,
                arguments => Log("search_files", () => folder.Search(
                    ConfigurationFunction.GetString(arguments, "query") ?? string.Empty,
                    ConfigurationFunction.GetString(arguments, "file_pattern"),
                    ConfigurationFunction.GetString(arguments, "path")))),
        ];
    }

    private string Log(string function, Func<string> action)
    {
        logger.LogInformation("Модель вызвала {Function}", function);
        return action();
    }
}
