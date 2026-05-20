using System.IO;

namespace FileCleaner.Services;

public static class AppDataPaths
{
    public static string DirectoryPath
    {
        get
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DownCleaner");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string SettingsPath => Path.Combine(DirectoryPath, "settings.json");
    public static string RecommendationProfilePath => Path.Combine(DirectoryPath, "recommendations.json");
    public static string ErrorHistoryPath => Path.Combine(DirectoryPath, "recent-errors.json");
    public static string ErrorLogPath => Path.Combine(DirectoryPath, "error.log");
}
