namespace BitvavoBot.App.Composition;

public static class AppPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitvavoBot");

    public static string DatabasePath => Path.Combine(DataDirectory, "bitvavobot.db");
    public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");
    public static string LogDirectory => Path.Combine(DataDirectory, "logs");
}
