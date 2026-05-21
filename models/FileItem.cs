using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace FileCleaner.Models;

public class FileItem : INotifyPropertyChanged
{
    private static readonly HashSet<string> PreviewExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp"
    };

    private static readonly System.Windows.Media.Brush LowRiskBrush = CreateBrush(33, 150, 243);
    private static readonly System.Windows.Media.Brush MediumRiskBrush = CreateBrush(255, 193, 7);
    private static readonly System.Windows.Media.Brush HighRiskBrush = CreateBrush(244, 67, 54);
    private static readonly System.Windows.Media.Brush NeutralRiskBrush = CreateBrush(117, 117, 117);

    private bool _isSelected;
    private string _fileName = "";
    private string _filePath = "";
    private long _fileSize;
    private DateTime _lastModified;
    private DateTime _lastAccessed;
    private int _riskScore;
    private string _riskReason = "";
    private string _associatedProgram = "";
    private bool _isInUse;
    private bool _needsUsageCheck;

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public string FileName
    {
        get => _fileName;
        set { _fileName = value; OnPropertyChanged(); }
    }

    public string FilePath
    {
        get => _filePath;
        set
        {
            _filePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPreviewSupported));
        }
    }

    public long FileSize
    {
        get => _fileSize;
        set { _fileSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileSizeDisplay)); }
    }

    public DateTime LastModified
    {
        get => _lastModified;
        set { _lastModified = value; OnPropertyChanged(); }
    }

    public DateTime LastAccessed
    {
        get => _lastAccessed;
        set { _lastAccessed = value; OnPropertyChanged(); }
    }

    public string RiskLevel
    {
        get => GetRiskLevel(RiskScore);
        set
        {
            // Kept for compatibility with existing scanner code. The displayed
            // level is always derived from RiskScore to avoid stale labels.
            OnPropertyChanged();
            OnPropertyChanged(nameof(RiskBrush));
            OnPropertyChanged(nameof(RiskDisplay));
        }
    }

    public int RiskScore
    {
        get => _riskScore;
        set
        {
            var next = Math.Clamp(value, 0, 100);
            if (_riskScore == next) return;

            _riskScore = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RiskLevel));
            OnPropertyChanged(nameof(RiskBrush));
            OnPropertyChanged(nameof(RiskDisplay));
        }
    }

    public string RiskDisplay => $"{RiskLevel} ({RiskScore}점)";

    public string RiskReason
    {
        get => _riskReason;
        set { _riskReason = value; OnPropertyChanged(); }
    }

    public string AssociatedProgram
    {
        get => _associatedProgram;
        set { _associatedProgram = value; OnPropertyChanged(); }
    }

    public bool IsInUse
    {
        get => _isInUse;
        set { _isInUse = value; OnPropertyChanged(); }
    }

    public bool NeedsUsageCheck
    {
        get => _needsUsageCheck;
        set
        {
            if (_needsUsageCheck == value) return;

            _needsUsageCheck = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RiskLevel));
            OnPropertyChanged(nameof(RiskBrush));
            OnPropertyChanged(nameof(RiskDisplay));
        }
    }

    public bool IsPreviewSupported
    {
        get
        {
            var ext = Path.GetExtension(FilePath);
            return !string.IsNullOrWhiteSpace(FilePath)
                && PreviewExtensions.Contains(ext)
                && File.Exists(FilePath);
        }
    }

    public string FileSizeDisplay
    {
        get
        {
            double s = FileSize;
            string[] u = { "B", "KB", "MB", "GB" };
            int i = 0;
            while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
            return $"{s:F1} {u[i]}";
        }
    }

    public System.Windows.Media.Brush RiskBrush => RiskLevel switch
    {
        "낮음 (삭제 후보)" => LowRiskBrush,
        "중간" => MediumRiskBrush,
        "높음 (삭제 주의)" => HighRiskBrush,
        _ => NeutralRiskBrush
    };

    public static string GetRiskLevel(int riskScore)
    {
        var score = Math.Clamp(riskScore, 0, 100);
        return score switch
        {
            >= 70 => "높음 (삭제 주의)",
            >= 40 => "중간",
            _ => "낮음 (삭제 후보)"
        };
    }

    private static System.Windows.Media.Brush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
