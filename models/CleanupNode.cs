using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace FileCleaner.Models;

public class CleanupNode : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isExpanded;
    private bool _isApplyingSelectionToChildren;
    private long _totalSize;
    private int _descendantFolderCount;
    private int _descendantFileCount;

    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsFolder { get; init; }
    public FileItem? File { get; init; }
    public CleanupNode? Parent { get; private set; }
    public ObservableCollection<CleanupNode> Children { get; } = new();

    public string Icon => IsFolder ? "D" : "F";
    public int Depth => Parent == null ? 0 : Parent.Depth + 1;
    public Thickness IndentMargin => new(Depth * 18, 0, 0, 0);
    public bool IsExpandable => Children.Count > 0;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => IsFolder ? _isSelected : File?.IsSelected ?? _isSelected;
        set => SetSelected(value, propagateToChildren: true);
    }

    public long TotalSize => IsFolder ? _totalSize : File?.FileSize ?? _totalSize;

    public string SizeDisplay
    {
        get
        {
            double size = TotalSize;
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            var unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:F1} {units[unitIndex]}";
        }
    }

    public int DescendantFolderCount => IsFolder ? _descendantFolderCount : 0;

    public int DescendantFileCount => IsFolder ? _descendantFileCount : 1;

    public string DetailText => IsFolder
        ? $"하위 폴더 {DescendantFolderCount}개 · 파일 {DescendantFileCount}개"
        : BuildFileDetail();

    public event PropertyChangedEventHandler? PropertyChanged;

    public CleanupNode()
    {
    }

    public CleanupNode(FileItem file)
    {
        File = file;
        Name = file.FileName;
        FullPath = file.FilePath;
        _totalSize = file.FileSize;
        _descendantFileCount = 1;
        file.PropertyChanged += File_PropertyChanged;
    }

    public void AddChild(CleanupNode child)
    {
        child.Parent = this;
        child.PropertyChanged += Child_PropertyChanged;
        Children.Add(child);
        OnPropertyChanged(nameof(IsExpandable));
        ApplyAggregateDelta(child.TotalSize, child.FolderContribution, child.FileContribution);
    }

    public void SortRecursive()
    {
        foreach (var child in Children)
            child.SortRecursive();

        var ordered = Children
            .OrderByDescending(child => child.IsFolder)
            .ThenBy(child => child.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Children.Clear();
        foreach (var child in ordered)
            Children.Add(child);
    }

    public void CompactSingleFolderChains()
    {
        foreach (var child in Children.ToList())
            child.CompactSelf();
    }

    private void CompactSelf()
    {
        foreach (var child in Children.ToList())
            child.CompactSelf();

        while (IsFolder && Children.Count == 1 && Children[0].IsFolder)
        {
            var onlyChild = Children[0];
            Name = $"{Name}\\{onlyChild.Name}";
            FullPath = onlyChild.FullPath;
            IsExpanded = onlyChild.IsExpanded;
            _totalSize = onlyChild._totalSize;
            _descendantFolderCount = onlyChild._descendantFolderCount;
            _descendantFileCount = onlyChild._descendantFileCount;

            Children.Clear();
            foreach (var grandChild in onlyChild.Children.ToList())
            {
                grandChild.Parent = this;
                Children.Add(grandChild);
            }
        }
    }

    private void SetSelected(bool value, bool propagateToChildren)
    {
        if (IsFolder)
        {
            if (_isSelected == value && (!propagateToChildren || Children.All(child => child.IsSelected == value)))
                return;

            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));

            if (propagateToChildren)
            {
                _isApplyingSelectionToChildren = true;
                foreach (var child in Children)
                    child.SetSelected(value, propagateToChildren: true);
                _isApplyingSelectionToChildren = false;
            }

            RefreshSelectionFromChildren();
            Parent?.RefreshSelectionFromChildren();
            return;
        }

        if (File != null)
        {
            if (File.IsSelected != value)
                File.IsSelected = value;
            else
                OnPropertyChanged(nameof(IsSelected));
        }
        else
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }

        Parent?.RefreshSelectionFromChildren();
    }

    private void RefreshSelectionFromChildren()
    {
        if (!IsFolder || _isApplyingSelectionToChildren)
            return;

        var newValue = Children.Count > 0 && Children.All(child => child.IsSelected);
        if (_isSelected == newValue)
            return;

        _isSelected = newValue;
        OnPropertyChanged(nameof(IsSelected));
        Parent?.RefreshSelectionFromChildren();
    }

    private void Child_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsSelected))
            RefreshSelectionFromChildren();
    }

    private void File_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileItem.IsSelected))
        {
            OnPropertyChanged(nameof(IsSelected));
            Parent?.RefreshSelectionFromChildren();
        }

        if (e.PropertyName == nameof(FileItem.RiskScore)
            || e.PropertyName == nameof(FileItem.RiskLevel)
            || e.PropertyName == nameof(FileItem.RiskDisplay)
            || e.PropertyName == nameof(FileItem.RiskReason)
            || e.PropertyName == nameof(FileItem.AssociatedProgram)
            || e.PropertyName == nameof(FileItem.IsInUse)
            || e.PropertyName == nameof(FileItem.NeedsUsageCheck))
        {
            OnPropertyChanged(nameof(DetailText));
        }

        if (e.PropertyName == nameof(FileItem.FileSize))
        {
            var newSize = File?.FileSize ?? 0;
            var delta = newSize - _totalSize;
            if (delta == 0)
                return;

            _totalSize = newSize;
            OnPropertyChanged(nameof(TotalSize));
            OnPropertyChanged(nameof(SizeDisplay));
            Parent?.ApplyAggregateDelta(delta, 0, 0);
        }
    }

    private string BuildFileDetail()
    {
        if (File == null)
            return "";

        if (File.IsInUse)
            return $"{File.RiskDisplay} · 사용 중";

        return string.IsNullOrWhiteSpace(File.AssociatedProgram)
            ? File.RiskDisplay
            : $"{File.RiskDisplay} · {File.AssociatedProgram}";
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private int FolderContribution => IsFolder ? 1 + DescendantFolderCount : 0;

    private int FileContribution => IsFolder ? DescendantFileCount : 1;

    private void ApplyAggregateDelta(long sizeDelta, int folderDelta, int fileDelta)
    {
        if (!IsFolder)
            return;

        _totalSize += sizeDelta;
        _descendantFolderCount += folderDelta;
        _descendantFileCount += fileDelta;

        OnPropertyChanged(nameof(TotalSize));
        OnPropertyChanged(nameof(SizeDisplay));
        OnPropertyChanged(nameof(DescendantFolderCount));
        OnPropertyChanged(nameof(DescendantFileCount));
        OnPropertyChanged(nameof(DetailText));

        Parent?.ApplyAggregateDelta(sizeDelta, folderDelta, fileDelta);
    }
}
