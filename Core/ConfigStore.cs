using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OwTranslateLite.Core;

public sealed class ConfigStore
{
    public const int CurrentDataSchemaVersion = 2;
    private const long RuntimeLogMaxBytes = 2L * 1024L * 1024L;
    private const long CrashLogMaxBytes = 1L * 1024L * 1024L;
    private const long DebugLogMaxBytes = 5L * 1024L * 1024L;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string AppDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OWTranslatorLite");

    public static string SettingsPath { get; } = Path.Combine(AppDirectory, "settings.json");
    public static string LogsDirectory { get; } = Path.Combine(AppDirectory, "logs");
    public static string DiagnosticsDirectory { get; } = Path.Combine(AppDirectory, "diagnostics");
    public static string RuntimeLogPath { get; } = Path.Combine(LogsDirectory, "runtime.log");
    public static string CrashLogPath { get; } = Path.Combine(LogsDirectory, "crash.log");
    public static string DebugLogPath { get; } = Path.Combine(LogsDirectory, "debug.log");
    public static string LegacyRuntimeLogPath { get; } = Path.Combine(AppDirectory, "runtime.log");
    public static string LegacyCrashLogPath { get; } = Path.Combine(AppDirectory, "crash.log");
    public static string LegacyDedupeLogPath { get; } = Path.Combine(AppDirectory, "dedupe.log");

    public AppSettings Settings { get; private set; } = new();

    public void Load()
    {
        InitializeDataLayout();
        if (!File.Exists(SettingsPath))
        {
            Save();
            return;
        }

        try
        {
            string json = File.ReadAllText(SettingsPath, Encoding.UTF8);
            Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            if (SettingsMigrator.MigrateAfterLoad(Settings).Changed)
            {
                Save();
            }
        }
        catch
        {
            Settings = new AppSettings();
            Save();
        }
    }

    public void Save()
    {
        InitializeDataLayout();
        SettingsMigrator.PrepareForSave(Settings);
        string json = JsonSerializer.Serialize(Settings, JsonOptions);
        File.WriteAllText(SettingsPath, json, new UTF8Encoding(false));
    }

    public void ResetUserData()
    {
        InitializeDataLayout();
        Settings = new AppSettings();

        DeleteIfExists(SettingsPath);
        DeleteFiles(LogsDirectory, "*.log");
        DeleteFiles(DiagnosticsDirectory, "*.txt");
        DeleteFiles(DiagnosticsDirectory, "*.zip");
        DeleteIfExists(LegacyRuntimeLogPath);
        DeleteIfExists(LegacyCrashLogPath);
        DeleteIfExists(LegacyDedupeLogPath);

        foreach (string path in Directory.EnumerateFiles(AppDirectory, "diagnostics-*.txt"))
        {
            DeleteIfExists(path);
        }

        Save();
    }

    public static void InitializeDataLayout()
    {
        Directory.CreateDirectory(AppDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(DiagnosticsDirectory);
        CopyLegacyLogIfNeeded(LegacyRuntimeLogPath, Path.Combine(LogsDirectory, "runtime.legacy.log"));
        CopyLegacyLogIfNeeded(LegacyCrashLogPath, Path.Combine(LogsDirectory, "crash.legacy.log"));
        CopyLegacyLogIfNeeded(LegacyDedupeLogPath, Path.Combine(LogsDirectory, "debug.legacy-dedupe.log"));
        RotateLogIfNeeded(RuntimeLogPath, RuntimeLogMaxBytes, 3);
        RotateLogIfNeeded(CrashLogPath, CrashLogMaxBytes, 5);
        RotateLogIfNeeded(DebugLogPath, DebugLogMaxBytes, 3);
    }

    private static void CopyLegacyLogIfNeeded(string sourcePath, string destinationPath)
    {
        try
        {
            if (File.Exists(sourcePath) && !File.Exists(destinationPath))
            {
                File.Copy(sourcePath, destinationPath);
            }
        }
        catch
        {
            // Legacy log migration is best-effort and must not block startup.
        }
    }

    private static void RotateLogIfNeeded(string path, long maxBytes, int retainedFiles)
    {
        try
        {
            FileInfo file = new(path);
            if (!file.Exists || file.Length <= maxBytes)
            {
                return;
            }

            for (int index = retainedFiles; index >= 1; index--)
            {
                string source = index == 1 ? path : GetRotatedLogPath(path, index - 1);
                string destination = GetRotatedLogPath(path, index);
                if (!File.Exists(source))
                {
                    continue;
                }

                DeleteIfExists(destination);
                File.Move(source, destination);
            }
        }
        catch
        {
            // Logging maintenance is best-effort.
        }
    }

    private static string GetRotatedLogPath(string path, int index)
    {
        string? directory = Path.GetDirectoryName(path);
        string fileName = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        return Path.Combine(directory ?? "", $"{fileName}.{index}{extension}");
    }

    private static void DeleteFiles(string directory, string pattern)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (string path in Directory.EnumerateFiles(directory, pattern))
            {
                DeleteIfExists(path);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup; the UI reports completion after the fresh settings file is written.
        }
    }
}
