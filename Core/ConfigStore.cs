using System.IO;
using System.Text;
using System.Text.Json;

namespace OwTranslateLite.Core;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string AppDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OWTranslatorLite");

    public static string SettingsPath { get; } = Path.Combine(AppDirectory, "settings.json");

    public AppSettings Settings { get; private set; } = new();

    public void Load()
    {
        Directory.CreateDirectory(AppDirectory);
        if (!File.Exists(SettingsPath))
        {
            Save();
            return;
        }

        try
        {
            string json = File.ReadAllText(SettingsPath, Encoding.UTF8);
            Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            Settings = new AppSettings();
            Save();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(AppDirectory);
        string json = JsonSerializer.Serialize(Settings, JsonOptions);
        File.WriteAllText(SettingsPath, json, new UTF8Encoding(false));
    }
}
