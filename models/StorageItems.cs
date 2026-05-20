using System.Windows.Media;

namespace FileCleaner.Models;

public class StorageItem
{
    public string Name       { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public long   Size       { get; set; }
    public double Percent    { get; set; }
    public System.Windows.Media.Brush  BarBrush   { get; set; } = System.Windows.Media.Brushes.Gray;

    public string SizeDisplay
    {
        get
        {
            double s = Size;
            string[] u = { "B", "KB", "MB", "GB" };
            int i = 0;
            while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
            return $"{s:F1} {u[i]}";
        }
    }
}
