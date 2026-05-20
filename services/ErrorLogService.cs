using System.IO;
using System.Text;
using System.Text.Json;
using FileCleaner.Models;

namespace FileCleaner.Services;

public static class ErrorLogService
{
    private const int MaxRecentErrors = 30;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static void LogException(string source, Exception ex)
    {
        try
        {
            var item = new ErrorHistoryItem
            {
                Time = DateTime.Now,
                Source = source,
                Type = ex.GetType().FullName ?? ex.GetType().Name,
                Message = ex.Message,
                StackTrace = ex.ToString()
            };

            AppendTextLog(item);
            SaveRecent(item);
        }
        catch
        {
            // Never throw from logger.
        }
    }

    public static List<ErrorHistoryItem> LoadRecent()
    {
        try
        {
            if (!File.Exists(AppDataPaths.ErrorHistoryPath))
                return new List<ErrorHistoryItem>();

            var json = File.ReadAllText(AppDataPaths.ErrorHistoryPath);
            return JsonSerializer.Deserialize<List<ErrorHistoryItem>>(json, JsonOptions)?
                .OrderByDescending(item => item.Time)
                .ToList()
                ?? new List<ErrorHistoryItem>();
        }
        catch
        {
            return new List<ErrorHistoryItem>();
        }
    }

    private static void SaveRecent(ErrorHistoryItem item)
    {
        var items = LoadRecent();
        items.Insert(0, item);
        items = items
            .OrderByDescending(x => x.Time)
            .Take(MaxRecentErrors)
            .ToList();

        var json = JsonSerializer.Serialize(items, JsonOptions);
        File.WriteAllText(AppDataPaths.ErrorHistoryPath, json);
    }

    private static void AppendTextLog(ErrorHistoryItem item)
    {
        var sb = new StringBuilder();
        sb.AppendLine("========================================");
        sb.AppendLine($"Time   : {item.Time:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"Source : {item.Source}");
        sb.AppendLine($"Type   : {item.Type}");
        sb.AppendLine($"Msg    : {item.Message}");
        sb.AppendLine("Stack  :");
        sb.AppendLine(item.StackTrace);
        sb.AppendLine();

        File.AppendAllText(AppDataPaths.ErrorLogPath, sb.ToString(), Encoding.UTF8);
    }
}
