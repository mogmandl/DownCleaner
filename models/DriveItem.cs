using System.Windows.Media;

namespace FileCleaner.Models;

public class DriveItem
{
    public string Name { get; set; } = "";
    public long TotalSize { get; set; }
    public long FreeSpace { get; set; }

    public long UsedSpace => TotalSize - FreeSpace;
    public double UsagePercent => TotalSize > 0 ? (double)UsedSpace / TotalSize * 100 : 0;

    public string UsageDisplay =>
        $"{Fmt(UsedSpace)} / {Fmt(TotalSize)} ({UsagePercent:F1}% 사용 중)";

    public System.Windows.Media.Brush UsageBrush => UsagePercent switch
    {
        >= 90 => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 57, 53)),
        >= 70 => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 140, 0)),
        _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(67, 160, 71))
    };

    private static string Fmt(long b)
    {
        if (b >= 1024L * 1024 * 1024)
            return $"{b / (1024.0 * 1024 * 1024):F1} GB";

        return $"{b / (1024.0 * 1024):F0} MB";
    }
}
