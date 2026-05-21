namespace FileCleaner.Models;

public sealed class ScanPolicy
{
    public const string DailyCleanup = "일상 정리";
    public const string LargeFileCleanup = "대용량 정리";
    public const string AdvancedCleanup = "고급 정리";
    public const string SafeReview = "안전 검토";

    public string ProfileName { get; init; } = DailyCleanup;
    public bool IncludeGeneratedFolders { get; init; }
    public bool PreferDownloadCleanup { get; init; }
    public bool PreferLargeOldFiles { get; init; }
    public bool DisableAutoSelect { get; init; }
    public bool ProtectProjectFiles { get; init; } = true;

    public string AutoSelectLabel => DisableAutoSelect
        ? "안전 검토 모드: 자동 선택 안 함"
        : $"{ProfileName}: 낮음 등급 후보 선택";

    public static ScanPolicy FromProfile(string? profileName)
    {
        return profileName switch
        {
            LargeFileCleanup => new ScanPolicy
            {
                ProfileName = LargeFileCleanup,
                PreferLargeOldFiles = true,
                PreferDownloadCleanup = true,
                ProtectProjectFiles = true
            },
            AdvancedCleanup => new ScanPolicy
            {
                ProfileName = AdvancedCleanup,
                IncludeGeneratedFolders = true,
                PreferLargeOldFiles = true,
                ProtectProjectFiles = true
            },
            SafeReview => new ScanPolicy
            {
                ProfileName = SafeReview,
                PreferDownloadCleanup = true,
                DisableAutoSelect = true,
                ProtectProjectFiles = true
            },
            _ => new ScanPolicy
            {
                ProfileName = DailyCleanup,
                PreferDownloadCleanup = true,
                ProtectProjectFiles = true
            }
        };
    }
}
