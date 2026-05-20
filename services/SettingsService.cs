using System.IO;
using System.Text.Json;
using FileCleaner.Models;

namespace FileCleaner.Services;

public static class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(AppDataPaths.SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(AppDataPaths.SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            ErrorLogService.LogException("Settings Load", ex);
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(AppDataPaths.SettingsPath, json);
        }
        catch (Exception ex)
        {
            ErrorLogService.LogException("Settings Save", ex);
        }
    }
}
