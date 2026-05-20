using System.IO;
using System.Text.Json;
using FileCleaner.Models;

namespace FileCleaner.Services;

public static class RecommendationProfileService
{
    private const int MaxAdjustment = 18;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static RecommendationProfile Load()
    {
        try
        {
            if (!File.Exists(AppDataPaths.RecommendationProfilePath))
                return new RecommendationProfile();

            var json = File.ReadAllText(AppDataPaths.RecommendationProfilePath);
            return JsonSerializer.Deserialize<RecommendationProfile>(json, JsonOptions) ?? new RecommendationProfile();
        }
        catch (Exception ex)
        {
            ErrorLogService.LogException("Recommendation Profile Load", ex);
            return new RecommendationProfile();
        }
    }

    public static void Save(RecommendationProfile profile)
    {
        try
        {
            profile.LastUpdated = DateTime.Now;
            var json = JsonSerializer.Serialize(profile, JsonOptions);
            File.WriteAllText(AppDataPaths.RecommendationProfilePath, json);
        }
        catch (Exception ex)
        {
            ErrorLogService.LogException("Recommendation Profile Save", ex);
        }
    }

    public static void RecordDeletedFiles(IEnumerable<FileItem> files)
    {
        var profile = Load();

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FilePath);
            if (string.IsNullOrWhiteSpace(ext))
                ext = "(no extension)";

            profile.DeletedExtensionCounts.TryGetValue(ext, out var count);
            profile.DeletedExtensionCounts[ext] = Math.Min(count + 1, 999);
        }

        Save(profile);
    }

    public static void ApplyLearnedPriority(IEnumerable<FileItem> files, RecommendationProfile profile)
    {
        if (profile.DeletedExtensionCounts.Count == 0)
            return;

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FilePath);
            if (string.IsNullOrWhiteSpace(ext))
                ext = "(no extension)";

            if (!profile.DeletedExtensionCounts.TryGetValue(ext, out var count) || count <= 0)
                continue;

            var adjustment = Math.Min(MaxAdjustment, 4 + count * 2);
            file.RiskScore = Math.Clamp(file.RiskScore - adjustment, 0, 100);
            file.RiskReason = AppendReason(file.RiskReason, $"삭제 이력 반영: {ext}");

            if (file.RiskScore < 40)
                file.RiskLevel = "낮음 (삭제 후보)";
            else if (file.RiskScore < 70)
                file.RiskLevel = "중간";
            else
                file.RiskLevel = "높음 (삭제 주의)";
        }
    }

    private static string AppendReason(string reason, string extra)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return extra;

        return reason.Contains(extra, StringComparison.OrdinalIgnoreCase)
            ? reason
            : $"{reason}, {extra}";
    }
}
