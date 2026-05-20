using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileCleaner.Models;

public class FileTreeNode : INotifyPropertyChanged
{
    private bool _isExpanded;

    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsFolder { get; set; }
    public FileItem? File { get; set; }
    public ObservableCollection<FileTreeNode> Children { get; set; } = new();

    public string Icon => IsFolder ? "📁" : "📄";

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
