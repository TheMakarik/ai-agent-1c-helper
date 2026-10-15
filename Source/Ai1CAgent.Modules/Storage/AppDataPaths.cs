namespace Ai1CAgent.Modules.Storage;

public static class AppDataPaths
{
    public const string AppFolderName = "Ai1CAgent";

    public static string Directory { get; } = Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppFolderName);

    public static string SettingsFile { get; } = Path.Join(Directory, "ai-providers.json");

    public static string ConfigurationPathFile { get; } = Path.Join(Directory, "configuration.json");

    public static string DatabaseFile { get; } = Path.Join(Directory, "ai1cagent.db");

    public static string SkillsGlobal { get; } = Path.Join(Directory, "Skills", "Global");

    public static string SkillsLocal { get; } = Path.Join(Directory, "Skills", "Local");

    public static string SecretKeyFile { get; } = Path.Join(Directory, "secret.key");
}
