using System.IO;
using System.Text.Json;
using ZCodePluginManager.Models;

namespace ZCodePluginManager.Services;

public static class SettingsService
{
    private static string DirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZCodePluginManager");

    private static string FilePath => Path.Combine(DirectoryPath, "settings.json");

    public static AppSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            return new AppSettings
            {
                PluginsRoot = PluginCatalog.ResolveDefaultRoot()
            };
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            if (string.IsNullOrWhiteSpace(settings.PluginsRoot))
            {
                settings.PluginsRoot = PluginCatalog.ResolveDefaultRoot();
            }

            return settings;
        }
        catch
        {
            return new AppSettings
            {
                PluginsRoot = PluginCatalog.ResolveDefaultRoot()
            };
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(FilePath, json);
    }
}
