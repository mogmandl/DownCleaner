using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileCleaner.Models;

public class FolderNode : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isLoading;

    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsProjectFolder { get; set; }
    public string ProjectType { get; set; } = "";
    public ObservableCollection<FolderNode> Children { get; set; } = new();

    public string Icon => IsProjectFolder ? ProjectType switch
    {
        "Git" => "G",
        "VisualStudio" => "VS",
        "NodeJS" => "N",
        "Python" => "Py",
        "Rust" => "Rs",
        "Go" => "Go",
        _ => "P"
    } : "D";

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
