using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileCleaner.Models;

public class AppSettings : INotifyPropertyChanged
{
    private int _autoSelectRiskThreshold = 40;
    private string _defaultScanMode = "Smart";
    private bool _includeSubfolders = true;
    private bool _showDetailedProgress = true;
    private bool _preferLearnedRecommendations = true;

    public int AutoSelectRiskThreshold
    {
        get => _autoSelectRiskThreshold;
        set
        {
            var next = Math.Clamp(value, 0, 100);
            if (_autoSelectRiskThreshold == next) return;
            _autoSelectRiskThreshold = next;
            OnPropertyChanged();
        }
    }

    public string DefaultScanMode
    {
        get => _defaultScanMode;
        set
        {
            var next = string.IsNullOrWhiteSpace(value) ? "Smart" : value;
            if (_defaultScanMode == next) return;
            _defaultScanMode = next;
            OnPropertyChanged();
        }
    }

    public bool IncludeSubfolders
    {
        get => _includeSubfolders;
        set
        {
            if (_includeSubfolders == value) return;
            _includeSubfolders = value;
            OnPropertyChanged();
        }
    }

    public bool ShowDetailedProgress
    {
        get => _showDetailedProgress;
        set
        {
            if (_showDetailedProgress == value) return;
            _showDetailedProgress = value;
            OnPropertyChanged();
        }
    }

    public bool PreferLearnedRecommendations
    {
        get => _preferLearnedRecommendations;
        set
        {
            if (_preferLearnedRecommendations == value) return;
            _preferLearnedRecommendations = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
