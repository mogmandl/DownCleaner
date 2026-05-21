using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using FileCleaner.Helpers;
using FileCleaner.Models;
using FileCleaner.Services;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace FileCleaner.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private const int LowRiskCandidateLimit = 40;

    private string _status = "폴더를 선택하거나 추가하세요.";
    private string _operationDetail = "";
    private bool _isScanning;
    private bool _isProgressIndeterminate = true;
    private double _scanProgress;
    private string _selectedInfo = "";
    private CancellationTokenSource? _cts;
    private string _currentScanRootPath = "";
    private string _currentScanRootName = "";
    private int _activeOperationId;
    private bool _suspendSelectionInfoRefresh;
    private bool _selectionInfoRefreshPending;
    private bool _selectionInfoRefreshQueued;
    private bool _suspendDeleteListRefresh;

    private AppSettings _settings;
    private RecommendationProfile _recommendationProfile;
    private readonly Dictionary<string, List<FileItem>> _preloadedScans = new(StringComparer.OrdinalIgnoreCase);
    private FileItem? _selectedFile;
    private CleanupNode? _selectedCleanupNode;
    private ImageSource? _previewImage;
    private string _previewText = "";
    private Model3DGroup? _previewModel;
    private Point3D _previewCameraPosition = new(0, 0, 6);
    private Vector3D _previewCameraLookDirection = new(0, 0, -6);
    private bool _isPreviewAvailable;
    private bool _isTextPreviewAvailable;
    private bool _isModelPreviewAvailable;
    private string _previewMessage = "이미지 파일을 선택하면 미리보기를 확인할 수 있습니다.";
    private double _previewYaw = 35;
    private double _previewPitch = 20;
    private double _previewDistance = 6;

    public ObservableCollection<FolderNode> RootNodes { get; } = new();
    public ObservableCollection<FolderNode> FavoriteFolders { get; } = new();
    public BulkObservableCollection<FileItem> CurrentFiles { get; } = new();
    public BulkObservableCollection<CleanupNode> CurrentCleanupNodes { get; } = new();
    public BulkObservableCollection<CleanupNode> DeleteListNodes { get; } = new();
    public ObservableCollection<FileItem> DeleteList { get; } = new();
    public ObservableCollection<ProjectFolderItem> ProjectFolders { get; } = new();
    public ObservableCollection<ChartItem> ProjectTypeItems { get; } = new();
    public ObservableCollection<DriveItem> DriveInfoList { get; } = new();
    public ObservableCollection<StorageItem> StorageItems { get; } = new();
    public ObservableCollection<ChartItem> DriveChartItems { get; } = new();
    public ObservableCollection<ChartItem> RiskDistributionItems { get; } = new();
    public ObservableCollection<ErrorHistoryItem> RecentErrors { get; } = new();
    public IReadOnlyList<string> ScanModes { get; } = new[] { "Detailed", "Quick" };

    public AppSettings Settings
    {
        get => _settings;
        private set
        {
            if (_settings == value) return;
            if (_settings != null)
                _settings.PropertyChanged -= Settings_PropertyChanged;

            _settings = value;
            _settings.PropertyChanged += Settings_PropertyChanged;
            OnPropertyChanged();
        }
    }

    public FileItem? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (_selectedFile == value) return;
            _selectedFile = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedEntryInfo));
            OnPropertyChanged(nameof(SelectedEntryDetail));
            UpdatePreview();
        }
    }

    public CleanupNode? SelectedCleanupNode
    {
        get => _selectedCleanupNode;
        set
        {
            if (_selectedCleanupNode == value) return;
            _selectedCleanupNode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedEntryName));
            OnPropertyChanged(nameof(SelectedEntryPath));
            OnPropertyChanged(nameof(SelectedEntryInfo));
            OnPropertyChanged(nameof(SelectedEntryDetail));

            SelectedFile = value is { IsFolder: false, File: not null } ? value.File : null;
            if (value?.IsFolder == true)
                UpdatePreview();
        }
    }

    public ImageSource? PreviewImage
    {
        get => _previewImage;
        set { _previewImage = value; OnPropertyChanged(); }
    }

    public bool IsPreviewAvailable
    {
        get => _isPreviewAvailable;
        set { _isPreviewAvailable = value; OnPropertyChanged(); }
    }

    public string PreviewText
    {
        get => _previewText;
        set { _previewText = value; OnPropertyChanged(); }
    }

    public bool IsTextPreviewAvailable
    {
        get => _isTextPreviewAvailable;
        set { _isTextPreviewAvailable = value; OnPropertyChanged(); }
    }

    public Model3DGroup? PreviewModel
    {
        get => _previewModel;
        set { _previewModel = value; OnPropertyChanged(); }
    }

    public Point3D PreviewCameraPosition
    {
        get => _previewCameraPosition;
        set { _previewCameraPosition = value; OnPropertyChanged(); }
    }

    public Vector3D PreviewCameraLookDirection
    {
        get => _previewCameraLookDirection;
        set { _previewCameraLookDirection = value; OnPropertyChanged(); }
    }

    public bool IsModelPreviewAvailable
    {
        get => _isModelPreviewAvailable;
        set { _isModelPreviewAvailable = value; OnPropertyChanged(); }
    }

    public string PreviewMessage
    {
        get => _previewMessage;
        set { _previewMessage = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string OperationDetail
    {
        get => _operationDetail;
        set { _operationDetail = value; OnPropertyChanged(); }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set { _isScanning = value; OnPropertyChanged(); }
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        set { _isProgressIndeterminate = value; OnPropertyChanged(); }
    }

    public double ScanProgress
    {
        get => _scanProgress;
        set { _scanProgress = Math.Clamp(value, 0, 100); OnPropertyChanged(); }
    }

    public string SelectedInfo
    {
        get => _selectedInfo;
        set { _selectedInfo = value; OnPropertyChanged(); }
    }

    public string DeleteListSummary =>
        $"삭제 목록: {DeleteList.Count}개 | 확보 가능 {Fmt(DeleteList.Sum(f => f.FileSize))}";

    public string SelectedEntryName => SelectedCleanupNode?.Name ?? "항목을 선택하세요";
    public string SelectedEntryPath => SelectedCleanupNode?.FullPath ?? "";

    public string SelectedEntryInfo
    {
        get
        {
            if (SelectedCleanupNode == null)
                return "";

            if (SelectedCleanupNode.IsFolder)
                return $"폴더 | {SelectedCleanupNode.DetailText} | {SelectedCleanupNode.SizeDisplay}";

            return SelectedFile == null
                ? ""
                : $"{SelectedFile.FileSizeDisplay} | {SelectedFile.RiskDisplay} | {SelectedFile.AssociatedProgram}";
        }
    }

    public string SelectedEntryDetail
    {
        get
        {
            if (SelectedCleanupNode == null)
                return "";

            return SelectedCleanupNode.IsFolder
                ? "폴더를 체크하면 하위 폴더와 파일이 함께 선택됩니다. 필요한 항목은 개별 해제할 수 있습니다."
                : SelectedFile?.RiskReason ?? "";
        }
    }

    public ICommand AddFolderCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand SelectDangerousCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand AddToDeleteListCommand { get; }
    public ICommand ClearDeleteListCommand { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand AnalyzeStorageCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand ReloadSettingsCommand { get; }
    public ICommand RefreshErrorsCommand { get; }

    public MainViewModel()
    {
        _settings = SettingsService.Load();
        _settings.PropertyChanged += Settings_PropertyChanged;
        _recommendationProfile = RecommendationProfileService.Load();

        AddFolderCommand = new AsyncRelayCommand(AddFolderAsync);
        RefreshCommand = new RelayCommand(Refresh);
        SelectAllCommand = new RelayCommand(() => SetAll(true));
        DeselectAllCommand = new RelayCommand(() => SetAll(false));
        SelectDangerousCommand = new RelayCommand(SelectDangerous);
        AddToDeleteListCommand = new RelayCommand(AddToDeleteList);
        ClearDeleteListCommand = new RelayCommand(ClearDeleteList);
        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync);
        AnalyzeStorageCommand = new AsyncRelayCommand(AnalyzeStorageAsync);
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        ReloadSettingsCommand = new RelayCommand(ReloadSettings);
        RefreshErrorsCommand = new RelayCommand(LoadRecentErrors);

        CurrentFiles.CollectionChanged += CurrentFiles_CollectionChanged;
        DeleteList.CollectionChanged += DeleteList_CollectionChanged;

        Refresh();
        LoadRecentErrors();
    }

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        SettingsService.Save(Settings);
        StatusMessage = "설정이 저장되었습니다.";
    }

    private void CurrentFiles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<FileItem>())
                item.PropertyChanged -= CurrentFile_PropertyChanged;
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<FileItem>())
                item.PropertyChanged += CurrentFile_PropertyChanged;
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
            RequestSelectionInfoRefresh();
    }

    private void CurrentFile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileItem.IsSelected))
            RequestSelectionInfoRefresh();

        if (e.PropertyName == nameof(FileItem.RiskScore)
            || e.PropertyName == nameof(FileItem.IsInUse)
            || e.PropertyName == nameof(FileItem.NeedsUsageCheck))
        {
            RefreshRiskDistribution();
        }
    }

    private void DeleteList_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_suspendDeleteListRefresh)
        {
            return;
        }

        RefreshDeleteListViews();
    }

    private void Refresh()
    {
        LoadDriveInfo();
        LoadQuickAccess();
        RefreshProjectFolders();
        LoadRecentErrors();
        StatusMessage = "새로고침 완료";
    }

    public async Task PreloadFavoriteFoldersAsync(IProgress<LoadingProgress>? progress = null, CancellationToken ct = default)
    {
        if (FavoriteFolders.Count == 0)
        {
            progress?.Report(new LoadingProgress("사전 분석할 즐겨찾기 폴더가 없습니다.", 100));
            return;
        }

        _preloadedScans.Clear();
        progress?.Report(new LoadingProgress($"시작 준비 중... 즐겨찾기 폴더 {FavoriteFolders.Count}개 분석 준비", 0));

        for (var i = 0; i < FavoriteFolders.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var folder = FavoriteFolders[i];
            if (string.IsNullOrWhiteSpace(folder.FullPath) || !Directory.Exists(folder.FullPath))
                continue;

            try
            {
                var startPercent = (double)i / FavoriteFolders.Count * 100;
                var endPercent = (double)(i + 1) / FavoriteFolders.Count * 100;
                progress?.Report(new LoadingProgress($"사전 분석 중 ({i + 1}/{FavoriteFolders.Count}): {folder.Name}", startPercent));
                var scanProgress = new Progress<string>(message =>
                    progress?.Report(new LoadingProgress($"{folder.Name}: {message}", startPercent)));
                var includeSubfolders = Settings.DefaultScanMode == "Quick"
                    ? false
                    : Settings.IncludeSubfolders;
                var files = await FileScanner.ScanFilesAsync(folder.FullPath, includeSubfolders, scanProgress, ct);

                ApplyLearnedRecommendations(files);
                _preloadedScans[folder.FullPath] = files;
                progress?.Report(new LoadingProgress($"사전 분석 완료 ({i + 1}/{FavoriteFolders.Count}): {folder.Name}", endPercent));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ErrorLogService.LogException($"Preload {folder.Name}", ex);
            }
        }

        progress?.Report(new LoadingProgress("시작 준비 완료", 100));
    }

    private void LoadDriveInfo()
    {
        DriveInfoList.Clear();
        foreach (var drive in StorageService.GetDriveInfo())
            DriveInfoList.Add(drive);

        RefreshDriveChart();
    }

    private void RefreshDriveChart()
    {
        DriveChartItems.Clear();
        foreach (var drive in DriveInfoList)
        {
            DriveChartItems.Add(new ChartItem
            {
                Label = drive.Name,
                Value = drive.UsedSpace,
                Percent = drive.UsagePercent,
                Display = drive.UsageDisplay,
                BarBrush = drive.UsageBrush
            });
        }
    }

    private void LoadQuickAccess()
    {
        RootNodes.Clear();
        FavoriteFolders.Clear();

        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, path) in StorageService.GetQuickAccessFolders())
        {
            if (!added.Add(path)) continue;

            var rootNode = CreateFolderNode(name, path);
            RootNodes.Add(rootNode);
            FavoriteFolders.Add(new FolderNode
            {
                Name = name,
                FullPath = path,
                IsProjectFolder = rootNode.IsProjectFolder,
                ProjectType = rootNode.ProjectType
            });
        }
    }

    private void RefreshProjectFolders()
    {
        var found = new Dictionary<string, ProjectFolderItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in RootNodes.Where(node => !string.IsNullOrWhiteSpace(node.FullPath)))
        {
            AddProjectFolderIfDetected(root, root.Name, found);
            AddLoadedProjectFolders(root, root.Name, found);
            AddDiscoveredProjectFolders(root, found);
        }

        var ordered = found.Values
            .OrderBy(item => item.ProjectType, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        ProjectFolders.Clear();
        foreach (var item in ordered)
            ProjectFolders.Add(item);

        RefreshProjectTypeSummary();
    }

    private static void AddLoadedProjectFolders(
        FolderNode node,
        string sourceName,
        IDictionary<string, ProjectFolderItem> found)
    {
        foreach (var child in node.Children.Where(child => !string.IsNullOrWhiteSpace(child.FullPath)))
        {
            AddProjectFolderIfDetected(child, sourceName, found);
            AddLoadedProjectFolders(child, sourceName, found);
        }
    }

    private static void AddDiscoveredProjectFolders(
        FolderNode root,
        IDictionary<string, ProjectFolderItem> found)
    {
        try
        {
            foreach (var (path, projectType) in FileScanner.FindProjectFolders(root.FullPath))
            {
                if (found.ContainsKey(path))
                    continue;

                found[path] = new ProjectFolderItem
                {
                    Name = Path.GetFileName(path) ?? path,
                    FolderPath = path,
                    ProjectType = projectType,
                    SourceName = root.Name
                };
            }
        }
        catch
        {
        }
    }

    private static void AddProjectFolderIfDetected(
        FolderNode node,
        string sourceName,
        IDictionary<string, ProjectFolderItem> found)
    {
        if (string.IsNullOrWhiteSpace(node.FullPath) || found.ContainsKey(node.FullPath))
            return;

        var projectType = node.ProjectType;
        var isProject = node.IsProjectFolder;

        if (!isProject)
        {
            var detected = FileScanner.DetectProjectType(node.FullPath);
            isProject = detected.IsProject;
            projectType = detected.ProjectType;
        }

        if (!isProject)
            return;

        found[node.FullPath] = new ProjectFolderItem
        {
            Name = node.Name,
            FolderPath = node.FullPath,
            ProjectType = projectType,
            SourceName = sourceName
        };
    }

    private void RefreshProjectTypeSummary()
    {
        ProjectTypeItems.Clear();

        var total = Math.Max(1, ProjectFolders.Count);
        Brush[] colors =
        {
            new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            new SolidColorBrush(Color.FromRgb(255, 193, 7)),
            new SolidColorBrush(Color.FromRgb(244, 67, 54)),
            new SolidColorBrush(Color.FromRgb(156, 39, 176)),
            new SolidColorBrush(Color.FromRgb(0, 188, 212))
        };

        var index = 0;
        foreach (var group in ProjectFolders.GroupBy(item => item.ProjectType).OrderByDescending(group => group.Count()))
        {
            var count = group.Count();
            ProjectTypeItems.Add(new ChartItem
            {
                Label = group.Key,
                Value = count,
                Percent = (double)count / total * 100,
                Display = $"{count}개",
                BarBrush = colors[index++ % colors.Length]
            });
        }
    }

    private FolderNode CreateFolderNode(string name, string fullPath)
    {
        var (isProject, projectType) = FileScanner.DetectProjectType(fullPath);
        var node = new FolderNode
        {
            Name = name,
            FullPath = fullPath,
            IsProjectFolder = isProject,
            ProjectType = projectType
        };

        if (HasSubFolders(fullPath))
            node.Children.Add(new FolderNode { Name = "...", FullPath = "" });

        return node;
    }

    private static bool HasSubFolders(string path)
    {
        try { return FileScanner.EnumerateDirectoriesSafely(path, recursive: false).Any(); }
        catch { return false; }
    }

    private async Task AddFolderAsync()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "추가할 폴더를 선택하세요.",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        var path = dialog.SelectedPath;
        var node = CreateFolderNode(Path.GetFileName(path) ?? path, path);
        RootNodes.Add(node);
        FavoriteFolders.Add(new FolderNode
        {
            Name = node.Name,
            FullPath = node.FullPath,
            IsProjectFolder = node.IsProjectFolder,
            ProjectType = node.ProjectType
        });
        RefreshProjectFolders();

        StatusMessage = node.IsProjectFolder
            ? $"'{node.Name}' 추가됨 [{node.ProjectType}]"
            : $"'{node.Name}' 추가됨";

        await Task.CompletedTask;
    }

    public async Task ExpandNodeAsync(FolderNode node)
    {
        if (node.Children.Count == 1 && string.IsNullOrEmpty(node.Children[0].FullPath))
        {
            var progress = new Progress<string>(ReportOperation);
            await FileScanner.LoadChildrenAsync(node, progress, CancellationToken.None);
            RefreshProjectFolders();
        }
    }

    private bool TryGetPreloadedFilesForFolder(string folderPath, out List<FileItem> files)
    {
        files = new List<FileItem>();
        if (string.IsNullOrWhiteSpace(folderPath))
            return false;

        var normalizedFolderPath = NormalizeDirectoryPath(folderPath);
        if (_preloadedScans.TryGetValue(normalizedFolderPath, out var exactFiles))
        {
            files = exactFiles;
            return true;
        }

        foreach (var (cachedRootPath, cachedFiles) in _preloadedScans)
        {
            var normalizedRootPath = NormalizeDirectoryPath(cachedRootPath);
            if (!IsSameOrChildPath(normalizedFolderPath, normalizedRootPath))
                continue;

            files = cachedFiles
                .Where(file => IsSameOrChildPath(file.FilePath, normalizedFolderPath))
                .ToList();
            return true;
        }

        return false;
    }

    public async Task LoadFilesForFolderAsync(FolderNode node)
    {
        if (TryGetPreloadedFilesForFolder(node.FullPath, out var cachedFiles))
        {
            ResetScanCollections();
            ResetSelectionDetails();
            _currentScanRootPath = node.FullPath;
            _currentScanRootName = node.Name;

            var cleanupRoot = BuildCleanupTree(node.Name, node.FullPath, cachedFiles, CancellationToken.None);
            ApplyScanResults(cachedFiles, cleanupRoot);
            StatusMessage = $"사전 분석 결과 로드: {cachedFiles.Count}개 파일 | 총 {Fmt(cachedFiles.Sum(file => file.FileSize))}";
            OperationDetail = "시작 시 준비한 분석 결과를 사용했습니다.";
            return;
        }

        var operationId = BeginOperation($"'{node.Name}' 스캔 중...", indeterminate: true);
        ResetScanCollections();
        _currentScanRootPath = node.FullPath;
        _currentScanRootName = node.Name;

        try
        {
            ResetSelectionDetails();
            var ct = _cts!.Token;
            var progress = new Progress<string>(ReportOperation);
            var includeSubfolders = Settings.DefaultScanMode == "Quick" ? false : Settings.IncludeSubfolders;
            var files = await FileScanner.ScanFilesAsync(node.FullPath, includeSubfolders, progress, ct);
            if (!IsCurrentOperation(operationId, ct))
                return;

            ApplyLearnedRecommendations(files);
            _preloadedScans[node.FullPath] = files;

            ReportOperation($"'{node.Name}' 폴더 구조 정리 중...");
            var cleanupRoot = await Task.Run(() => BuildCleanupTree(node.Name, node.FullPath, files, ct), ct);
            if (!IsCurrentOperation(operationId, ct))
                return;

            ApplyScanResults(files, cleanupRoot);

            StatusMessage = $"스캔 완료: {files.Count}개 파일 | 총 {Fmt(files.Sum(file => file.FileSize))}";
            OperationDetail = "위험도 분포와 추천 우선순위가 갱신되었습니다.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "스캔 취소됨";
        }
        catch (Exception ex)
        {
            ErrorLogService.LogException("Folder Scan", ex);
            StatusMessage = $"스캔 실패: {ex.Message}";
        }
        finally
        {
            EndBusy(operationId);
        }
    }

    private static string NormalizeDirectoryPath(string path)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool IsSameOrChildPath(string path, string rootPath)
    {
        var normalizedPath = Path.GetFullPath(path);
        var normalizedRoot = NormalizeDirectoryPath(rootPath);

        return normalizedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(normalizedRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private void SetAll(bool selected)
    {
        PerformSelectionInfoBatch(() =>
        {
            foreach (var file in CurrentFiles)
                file.IsSelected = selected;
        });
    }

    private void SelectDangerous()
    {
        var limit = Math.Min(Settings.AutoSelectRiskThreshold, LowRiskCandidateLimit);
        PerformSelectionInfoBatch(() =>
        {
            foreach (var file in CurrentFiles)
                file.IsSelected = IsLowRiskDeletionCandidate(file, limit);
        });

        StatusMessage = $"낮음 등급 삭제 후보만 선택했습니다. 기준: {limit}점 미만";
    }

    private void UpdateInfo()
    {
        var selectedCount = CurrentFiles.Count(file => file.IsSelected);
        var selectedSize = CurrentFiles.Where(file => file.IsSelected).Sum(file => file.FileSize);
        SelectedInfo = selectedCount > 0 ? $"선택: {selectedCount}개 / {Fmt(selectedSize)}" : "";
    }

    private void RequestSelectionInfoRefresh()
    {
        if (_suspendSelectionInfoRefresh)
        {
            _selectionInfoRefreshPending = true;
            return;
        }

        QueueSelectionInfoRefresh();
    }

    private void QueueSelectionInfoRefresh()
    {
        if (_selectionInfoRefreshQueued)
            return;

        _selectionInfoRefreshQueued = true;
        if (System.Windows.Application.Current?.Dispatcher == null)
        {
            _selectionInfoRefreshQueued = false;
            UpdateInfo();
            return;
        }

        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            _selectionInfoRefreshQueued = false;
            UpdateInfo();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void PerformSelectionInfoBatch(Action action)
    {
        var wasSuspended = _suspendSelectionInfoRefresh;
        _suspendSelectionInfoRefresh = true;

        try { action(); }
        finally
        {
            _suspendSelectionInfoRefresh = wasSuspended;
            if (!wasSuspended && _selectionInfoRefreshPending)
            {
                _selectionInfoRefreshPending = false;
                QueueSelectionInfoRefresh();
            }
        }
    }

    private void AddToDeleteList()
    {
        var selectedFiles = CurrentFiles.Where(file => file.IsSelected).ToList();
        var safeCandidates = selectedFiles
            .Where(file => IsLowRiskDeletionCandidate(file, LowRiskCandidateLimit))
            .ToList();
        var reviewRequired = selectedFiles.Except(safeCandidates).ToList();

        if (reviewRequired.Count > 0)
        {
            var preview = string.Join("\n", reviewRequired.Take(5).Select(file => $"  - {file.FileName} ({file.RiskDisplay})"));
            if (reviewRequired.Count > 5)
                preview += $"\n  ... 외 {reviewRequired.Count - 5}개";

            var result = System.Windows.MessageBox.Show(
                $"선택 항목 중 중간/높음 또는 사용 중 파일 {reviewRequired.Count}개가 포함되어 있습니다.\n\n{preview}\n\n이 파일들도 삭제 목록에 포함할까요?",
                "삭제 목록 추가 확인",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Cancel)
            {
                StatusMessage = "삭제 목록 추가를 취소했습니다.";
                return;
            }

            selectedFiles = result == MessageBoxResult.Yes
                ? selectedFiles
                : safeCandidates;
        }

        var added = 0;
        var existingPaths = DeleteList
            .Select(file => file.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        PerformDeleteListBatch(() =>
        {
            foreach (var file in selectedFiles)
            {
                if (!existingPaths.Add(file.FilePath))
                    continue;

                DeleteList.Add(file);
                added++;
            }
        });

        StatusMessage = $"삭제 목록에 {added}개 추가됨 (총 {DeleteList.Count}개)";
    }

    private static bool IsLowRiskDeletionCandidate(FileItem file, int limit)
        => file.RiskScore < Math.Min(limit, LowRiskCandidateLimit)
            && !file.IsInUse;

    private void ClearDeleteList()
    {
        PerformDeleteListBatch(DeleteList.Clear);
        StatusMessage = "삭제 목록을 초기화했습니다.";
    }

    private async Task DeleteSelectedAsync()
    {
        var targets = DeleteList.Where(file => file.IsSelected).ToList();
        if (!targets.Any())
        {
            System.Windows.MessageBox.Show("삭제할 파일을 체크해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var preview = string.Join("\n", targets.Take(5).Select(file => $"  - {file.FileName}"));
        if (targets.Count > 5)
            preview += $"\n  ... 외 {targets.Count - 5}개";

        var result = System.Windows.MessageBox.Show(
            $"선택한 {targets.Count}개 파일 ({Fmt(targets.Sum(file => file.FileSize))})을 휴지통으로 이동할까요?\n\n{preview}",
            "삭제 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        var operationId = BeginOperation("휴지통으로 이동 중...", indeterminate: false);

        try
        {
            var inUseTargets = targets.Where(file => file.IsInUse).ToList();
            if (inUseTargets.Count > 0)
            {
                targets = targets.Except(inUseTargets).ToList();
                System.Windows.MessageBox.Show(
                    $"사용 중인 파일 {inUseTargets.Count}개는 제외했습니다.",
                    "사용 중인 파일 제외",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                if (targets.Count == 0)
                {
                    StatusMessage = "이동할 수 있는 파일이 없습니다.";
                    return;
                }
            }

            var (success, error) = await Task.Run(() => RecycleBinService.SendToRecycleBin(targets.Select(file => file.FilePath)));
            if (success)
            {
                RecommendationProfileService.RecordDeletedFiles(targets);
                _recommendationProfile = RecommendationProfileService.Load();

                PerformDeleteListBatch(() =>
                {
                    foreach (var file in targets)
                    {
                        DeleteList.Remove(file);
                        CurrentFiles.Remove(file);
                    }
                });

                UpdateInfo();
                RebuildCleanupNodes(_currentScanRootName, _currentScanRootPath);
                RefreshRiskDistribution();
                ScanProgress = 100;
                StatusMessage = $"{targets.Count}개 파일을 휴지통으로 이동했습니다.";
                OperationDetail = "삭제 이력이 로컬 추천 프로필에 반영되었습니다.";
            }
            else
            {
                StatusMessage = $"삭제 실패: {error}";
                System.Windows.MessageBox.Show($"오류 발생:\n{error}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            ErrorLogService.LogException("Delete Selected", ex);
            StatusMessage = $"삭제 실패: {ex.Message}";
        }
        finally
        {
            EndBusy(operationId);
        }
    }

    private async Task AnalyzeStorageAsync()
    {
        var operationId = BeginOperation("저장공간 분석 중...", indeterminate: true);
        StorageItems.Clear();

        try
        {
            var quickAccess = StorageService.GetQuickAccessFolders();
            var quickPaths = quickAccess.Select(x => x.FolderPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var paths = quickAccess.Select(folder => folder.FolderPath).ToList();

            foreach (var node in RootNodes.Where(node => !string.IsNullOrWhiteSpace(node.FullPath)))
            {
                if (!quickPaths.Contains(node.FullPath))
                    paths.Add(node.FullPath);
            }

            var progress = new Progress<string>(ReportOperation);
            var items = await StorageService.AnalyzeFolderSizesAsync(paths, progress, _cts!.Token);
            if (!IsCurrentOperation(operationId, _cts!.Token))
                return;

            Brush[] colors =
            {
                new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                new SolidColorBrush(Color.FromRgb(156, 39, 176)),
                new SolidColorBrush(Color.FromRgb(0, 188, 212))
            };

            for (var i = 0; i < items.Count; i++)
                items[i].BarBrush = colors[i % colors.Length];

            foreach (var item in items)
                StorageItems.Add(item);

            LoadDriveInfo();
            StatusMessage = $"분석 완료: {items.Count}개 폴더";
            OperationDetail = "드라이브 사용량, 큰 폴더, 현재 스캔 위험 파일 비율을 갱신했습니다.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "분석 취소됨";
        }
        catch (Exception ex)
        {
            ErrorLogService.LogException("Storage Analysis", ex);
            StatusMessage = $"분석 실패: {ex.Message}";
            System.Windows.MessageBox.Show($"저장공간 분석 중 오류 발생:\n{ex.Message}", "분석 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            EndBusy(operationId);
        }
    }

    private void ApplyLearnedRecommendations(IReadOnlyCollection<FileItem> files)
    {
        if (Settings.PreferLearnedRecommendations)
            RecommendationProfileService.ApplyLearnedPriority(files, _recommendationProfile);
    }

    private void RefreshRiskDistribution()
    {
        RiskDistributionItems.Clear();
        var total = Math.Max(1, CurrentFiles.Count);
        var groups = new[]
        {
            new { Label = "삭제 후보", Count = CurrentFiles.Count(f => f.RiskScore < LowRiskCandidateLimit && !f.IsInUse), Brush = new SolidColorBrush(Color.FromRgb(33, 150, 243)) },
            new { Label = "주의", Count = CurrentFiles.Count(f => f.RiskScore >= LowRiskCandidateLimit && f.RiskScore < 70 && !f.IsInUse), Brush = new SolidColorBrush(Color.FromRgb(255, 193, 7)) },
            new { Label = "보존 권장", Count = CurrentFiles.Count(f => f.RiskScore >= 70 || f.IsInUse), Brush = new SolidColorBrush(Color.FromRgb(244, 67, 54)) }
        };

        foreach (var group in groups)
        {
            RiskDistributionItems.Add(new ChartItem
            {
                Label = group.Label,
                Value = group.Count,
                Percent = (double)group.Count / total * 100,
                Display = $"{group.Count}개 ({(double)group.Count / total * 100:F1}%)",
                BarBrush = group.Brush
            });
        }
    }

    private void SaveSettings()
    {
        SettingsService.Save(Settings);
        StatusMessage = "설정을 저장했습니다.";
    }

    private void ReloadSettings()
    {
        Settings = SettingsService.Load();
        StatusMessage = "설정을 다시 불러왔습니다.";
    }

    private void LoadRecentErrors()
    {
        RecentErrors.Clear();
        foreach (var item in ErrorLogService.LoadRecent())
            RecentErrors.Add(item);
    }

    private void Cancel()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
    }

    public void SetSelectedFolder(FolderNode? node)
    {
        // Keep for compatibility with existing code-behind event wiring.
    }

    public void SetSelectedCleanupNode(CleanupNode? node)
        => SelectedCleanupNode = node;

    public void AddCleanupNodeToDeleteList(CleanupNode? node)
    {
        if (node == null)
            return;

        var files = EnumerateNodeFiles(node).ToList();
        if (files.Count == 0)
        {
            StatusMessage = "삭제 목록에 추가할 파일이 없습니다.";
            return;
        }

        var safeCandidates = files
            .Where(file => IsLowRiskDeletionCandidate(file, LowRiskCandidateLimit))
            .ToList();
        var reviewRequired = files.Except(safeCandidates).ToList();

        if (reviewRequired.Count > 0)
        {
            var preview = string.Join("\n", reviewRequired.Take(5).Select(file => $"  - {file.FileName} ({file.RiskDisplay})"));
            if (reviewRequired.Count > 5)
                preview += $"\n  ... 외 {reviewRequired.Count - 5}개";

            var result = System.Windows.MessageBox.Show(
                $"이 폴더/항목에는 중간/높음 또는 사용 중 파일 {reviewRequired.Count}개가 포함되어 있습니다.\n\n{preview}\n\n이 파일들도 삭제 목록에 포함할까요?",
                "삭제 목록 추가 확인",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Cancel)
            {
                StatusMessage = "삭제 목록 추가를 취소했습니다.";
                return;
            }

            files = result == MessageBoxResult.Yes
                ? files
                : safeCandidates;
        }

        var existingPaths = DeleteList
            .Select(file => file.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;

        PerformDeleteListBatch(() =>
        {
            foreach (var file in files)
            {
                file.IsSelected = true;
                if (!existingPaths.Add(file.FilePath))
                    continue;

                DeleteList.Add(file);
                added++;
            }
        });

        StatusMessage = $"삭제 목록에 {added}개 추가됨 (총 {DeleteList.Count}개)";
    }

    public void RemoveCleanupNodeFromDeleteList(CleanupNode? node)
    {
        if (node == null)
            return;

        var removePaths = EnumerateNodeFiles(node)
            .Select(file => file.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (removePaths.Count == 0)
        {
            StatusMessage = "삭제 목록에서 제거할 파일이 없습니다.";
            return;
        }

        var removed = 0;
        PerformDeleteListBatch(() =>
        {
            foreach (var file in DeleteList.Where(file => removePaths.Contains(file.FilePath)).ToList())
            {
                DeleteList.Remove(file);
                removed++;
            }
        });

        StatusMessage = $"삭제 목록에서 {removed}개 제거됨 (총 {DeleteList.Count}개)";
    }

    private static IEnumerable<FileItem> EnumerateNodeFiles(CleanupNode node)
    {
        if (!node.IsFolder && node.File != null)
        {
            yield return node.File;
            yield break;
        }

        foreach (var child in node.Children)
        {
            foreach (var file in EnumerateNodeFiles(child))
                yield return file;
        }
    }

    public void RotatePreviewModel(double deltaX, double deltaY)
    {
        if (!IsModelPreviewAvailable)
            return;

        _previewYaw += deltaX * 0.45;
        _previewPitch = Math.Clamp(_previewPitch - deltaY * 0.45, -80, 80);
        UpdatePreviewCamera();
    }

    public void ZoomPreviewModel(double wheelDelta)
    {
        if (!IsModelPreviewAvailable)
            return;

        var factor = wheelDelta > 0 ? 0.88 : 1.12;
        _previewDistance = Math.Clamp(_previewDistance * factor, 1.5, 30);
        UpdatePreviewCamera();
    }

    public void ResetPreviewCamera()
    {
        _previewYaw = 35;
        _previewPitch = 20;
        _previewDistance = 6;
        UpdatePreviewCamera();
    }

    private void ResetScanCollections()
    {
        ReplaceCurrentFiles(Array.Empty<FileItem>());
        CurrentCleanupNodes.ReplaceRange(Array.Empty<CleanupNode>());
        SelectedInfo = "";
        RefreshRiskDistribution();
    }

    private void ReplaceCurrentFiles(IEnumerable<FileItem> files)
    {
        var currentFiles = CurrentFiles.ToList();
        foreach (var file in currentFiles)
            file.PropertyChanged -= CurrentFile_PropertyChanged;

        var replacement = files.ToList();
        CurrentFiles.ReplaceRange(replacement);

        foreach (var file in replacement)
            file.PropertyChanged += CurrentFile_PropertyChanged;

        UpdateInfo();
        RefreshRiskDistribution();
    }

    private void ApplyScanResults(IReadOnlyCollection<FileItem> files, CleanupNode cleanupRoot)
    {
        ReplaceCurrentFiles(files);
        CurrentCleanupNodes.ReplaceRange(new[] { cleanupRoot });

        if (SelectedCleanupNode != null && !string.IsNullOrWhiteSpace(SelectedCleanupNode.FullPath))
            SelectedCleanupNode = FindCleanupNode(cleanupRoot, SelectedCleanupNode.FullPath);
    }

    private void RebuildCleanupNodes(string rootName, string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            CurrentCleanupNodes.ReplaceRange(Array.Empty<CleanupNode>());
            return;
        }

        var rootNode = BuildCleanupTree(rootName, rootPath, CurrentFiles.ToList(), CancellationToken.None);
        CurrentCleanupNodes.ReplaceRange(new[] { rootNode });

        if (SelectedCleanupNode != null && !string.IsNullOrWhiteSpace(SelectedCleanupNode.FullPath))
            SelectedCleanupNode = FindCleanupNode(rootNode, SelectedCleanupNode.FullPath);
    }

    private void RebuildDeleteListNodes()
    {
        if (DeleteList.Count == 0)
        {
            DeleteListNodes.ReplaceRange(Array.Empty<CleanupNode>());
            return;
        }

        var rootPath = FindCommonRootPath(DeleteList.Select(file => file.FilePath));
        var rootName = string.IsNullOrWhiteSpace(rootPath)
            ? "삭제 목록"
            : Path.GetFileName(Path.TrimEndingDirectorySeparator(rootPath));

        if (string.IsNullOrWhiteSpace(rootName))
            rootName = rootPath;

        var rootNode = BuildCleanupTree(rootName, rootPath, DeleteList.ToList(), CancellationToken.None);
        DeleteListNodes.ReplaceRange(new[] { rootNode });
    }

    private void PerformDeleteListBatch(Action action)
    {
        var wasSuspended = _suspendDeleteListRefresh;
        _suspendDeleteListRefresh = true;

        try
        {
            action();
        }
        finally
        {
            _suspendDeleteListRefresh = wasSuspended;

            if (!wasSuspended)
            {
                RefreshDeleteListViews();
            }
        }
    }

    private void RefreshDeleteListViews()
    {
        OnPropertyChanged(nameof(DeleteListSummary));
        RebuildDeleteListNodes();
    }

    private CleanupNode BuildCleanupTree(
        string rootName,
        string rootPath,
        IReadOnlyCollection<FileItem> files,
        CancellationToken ct)
    {
        var rootNode = new CleanupNode
        {
            Name = rootName,
            FullPath = rootPath,
            IsFolder = true,
            IsExpanded = true
        };

        var folderMap = new Dictionary<string, CleanupNode>(StringComparer.OrdinalIgnoreCase)
        {
            [rootPath] = rootNode
        };

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var parentPath = Path.GetDirectoryName(file.FilePath) ?? rootPath;
            var parentNode = EnsureFolderNode(rootNode, folderMap, parentPath, rootPath);
            parentNode.AddChild(new CleanupNode(file));
        }

        rootNode.SortRecursive();
        rootNode.CompactSingleFolderChains();
        return rootNode;
    }

    private static CleanupNode EnsureFolderNode(
        CleanupNode rootNode,
        IDictionary<string, CleanupNode> folderMap,
        string targetPath,
        string rootPath)
    {
        if (folderMap.TryGetValue(targetPath, out var existing))
            return existing;

        var relativePath = Path.GetRelativePath(rootPath, targetPath);
        var parts = relativePath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(part => !string.IsNullOrWhiteSpace(part) && part != ".")
            .ToList();

        var currentNode = rootNode;
        var currentPath = rootPath;

        foreach (var part in parts)
        {
            currentPath = Path.Combine(currentPath, part);
            if (folderMap.TryGetValue(currentPath, out var mapped))
            {
                currentNode = mapped;
                continue;
            }

            var newNode = new CleanupNode
            {
                Name = part,
                FullPath = currentPath,
                IsFolder = true
            };

            currentNode.AddChild(newNode);
            folderMap[currentPath] = newNode;
            currentNode = newNode;
        }

        return currentNode;
    }

    private static string FindCommonRootPath(IEnumerable<string> filePaths)
    {
        var directories = filePaths
            .Select(path => Path.GetDirectoryName(path))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path!)))
            .ToList();

        if (directories.Count == 0)
            return "";

        var common = directories[0];
        foreach (var directory in directories.Skip(1))
        {
            while (!string.IsNullOrWhiteSpace(common)
                && !directory.StartsWith(common + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(directory, common, StringComparison.OrdinalIgnoreCase))
            {
                var parent = Path.GetDirectoryName(common);
                if (string.IsNullOrWhiteSpace(parent) || parent == common)
                    return Path.GetPathRoot(common) ?? "";

                common = Path.TrimEndingDirectorySeparator(parent);
            }
        }

        return common;
    }

    private static CleanupNode? FindCleanupNode(CleanupNode root, string path)
    {
        if (string.Equals(root.FullPath, path, StringComparison.OrdinalIgnoreCase))
            return root;

        foreach (var child in root.Children)
        {
            var found = FindCleanupNode(child, path);
            if (found != null)
                return found;
        }

        return null;
    }

    private void ResetSelectionDetails()
    {
        SelectedCleanupNode = null;
        SelectedFile = null;
        ClearPreview();
        PreviewMessage = "이미지 파일을 선택하면 미리보기를 확인할 수 있습니다.";
    }

    private void UpdatePreview()
    {
        ClearPreview();

        if (SelectedCleanupNode?.IsFolder == true)
        {
            PreviewMessage = "폴더는 미리보기를 지원하지 않습니다.";
            return;
        }

        if (SelectedFile == null)
        {
            PreviewMessage = "이미지 파일, 텍스트 파일, OBJ/STL 3D 파일을 선택하면 미리보기를 확인할 수 있습니다.";
            return;
        }

        var ext = Path.GetExtension(SelectedFile.FilePath).ToLowerInvariant();
        var fileName = Path.GetFileName(SelectedFile.FilePath);
        if (!File.Exists(SelectedFile.FilePath))
        {
            PreviewMessage = "파일을 찾을 수 없습니다.";
            return;
        }

        try
        {
            if (IsImageExtension(ext))
            {
                LoadImagePreview(SelectedFile.FilePath);
                return;
            }

            if (ext == ".obj")
            {
                LoadObjPreview(SelectedFile.FilePath);
                return;
            }

            if (ext == ".stl")
            {
                LoadStlPreview(SelectedFile.FilePath);
                return;
            }

            if (IsTextLikeFile(SelectedFile.FilePath, fileName, ext))
            {
                LoadTextPreview(SelectedFile.FilePath);
                return;
            }

            if (IsKnown3DExtension(ext))
            {
                PreviewMessage = $"{ext.ToUpperInvariant()} 파일은 외부 3D 로더가 필요합니다. 현재 내장 미리보기는 OBJ, STL 파일을 지원합니다.";
                return;
            }

            PreviewMessage = "미리보기를 지원하지 않는 파일입니다.";
        }
        catch (Exception ex)
        {
            ErrorLogService.LogException("Preview Load", ex);
            ClearPreview();
            PreviewMessage = "미리보기를 로드할 수 없습니다.";
        }
    }

    private void ClearPreview()
    {
        PreviewImage = null;
        PreviewText = "";
        PreviewModel = null;
        IsPreviewAvailable = false;
        IsTextPreviewAvailable = false;
        IsModelPreviewAvailable = false;
        PreviewMessage = "";
    }

    private void LoadImagePreview(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        bitmap.EndInit();
        bitmap.Freeze();

        PreviewImage = bitmap;
        IsPreviewAvailable = true;
    }

    private void LoadTextPreview(string path)
    {
        const int maxChars = 80_000;
        var info = new FileInfo(path);
        using var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[maxChars + 1];
        var read = reader.ReadBlock(buffer, 0, buffer.Length);
        var text = new string(buffer, 0, Math.Min(read, maxChars));

        if (text.IndexOf('\0') >= 0)
        {
            PreviewMessage = "바이너리로 보이는 파일이라 텍스트 미리보기를 표시하지 않았습니다.";
            return;
        }

        PreviewText = read > maxChars || info.Length > maxChars * 4
            ? text + Environment.NewLine + Environment.NewLine + "... 미리보기는 앞부분만 표시됩니다."
            : text;
        IsTextPreviewAvailable = true;
    }

    private void LoadObjPreview(string path)
    {
        var model = CreateObjModel(path);
        if (model.Children.Count == 0)
        {
            PreviewMessage = "표시할 수 있는 OBJ 메시를 찾지 못했습니다.";
            return;
        }

        PreviewModel = model;
        IsModelPreviewAvailable = true;
        ResetPreviewCamera();
    }

    private void LoadStlPreview(string path)
    {
        var model = CreateStlModel(path);
        if (model.Children.Count == 0)
        {
            PreviewMessage = "표시할 수 있는 STL 메시를 찾지 못했습니다.";
            return;
        }

        PreviewModel = model;
        IsModelPreviewAvailable = true;
        ResetPreviewCamera();
    }

    private static Model3DGroup CreateObjModel(string path)
    {
        const int maxVertices = 60_000;
        const int maxFaces = 80_000;
        var vertices = new List<Point3D>();
        var triangles = new Int32Collection();
        var faceCount = 0;

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;

            if (parts[0] == "v" && parts.Length >= 4 && vertices.Count < maxVertices)
            {
                if (TryParseDouble(parts[1], out var x)
                    && TryParseDouble(parts[2], out var y)
                    && TryParseDouble(parts[3], out var z))
                {
                    vertices.Add(new Point3D(x, y, z));
                }
            }
            else if (parts[0] == "f" && parts.Length >= 4 && faceCount < maxFaces)
            {
                var face = parts
                    .Skip(1)
                    .Select(part => ParseObjIndex(part, vertices.Count))
                    .Where(index => index >= 0 && index < vertices.Count)
                    .ToList();

                for (var i = 1; i + 1 < face.Count; i++)
                {
                    triangles.Add(face[0]);
                    triangles.Add(face[i]);
                    triangles.Add(face[i + 1]);
                    faceCount++;

                    if (faceCount >= maxFaces)
                        break;
                }
            }
        }

        var group = new Model3DGroup();
        if (vertices.Count == 0 || triangles.Count == 0)
            return group;

        NormalizePoints(vertices);

        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection(vertices),
            TriangleIndices = triangles
        };

        var material = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(80, 140, 210)));
        group.Children.Add(new AmbientLight(Colors.Gray));
        group.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-0.4, -0.6, -1)));
        group.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
        group.Freeze();
        return group;
    }

    private static Model3DGroup CreateStlModel(string path)
    {
        var vertices = new List<Point3D>();
        var triangles = new Int32Collection();

        if (LooksLikeBinaryStl(path))
            ReadBinaryStl(path, vertices, triangles);
        else
            ReadAsciiStl(path, vertices, triangles);

        var group = new Model3DGroup();
        if (vertices.Count == 0 || triangles.Count == 0)
            return group;

        NormalizePoints(vertices);

        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection(vertices),
            TriangleIndices = triangles
        };

        var material = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(90, 165, 120)));
        group.Children.Add(new AmbientLight(Colors.Gray));
        group.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-0.4, -0.6, -1)));
        group.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
        group.Freeze();
        return group;
    }

    private static bool LooksLikeBinaryStl(string path)
    {
        var info = new FileInfo(path);
        if (info.Length < 84)
            return false;

        using var stream = File.OpenRead(path);
        stream.Position = 80;
        Span<byte> countBytes = stackalloc byte[4];
        if (stream.Read(countBytes) != 4)
            return false;

        var triangleCount = BitConverter.ToUInt32(countBytes);
        return 84L + triangleCount * 50L == info.Length;
    }

    private static void ReadBinaryStl(string path, List<Point3D> vertices, Int32Collection triangles)
    {
        const int maxFaces = 80_000;
        using var reader = new BinaryReader(File.OpenRead(path));
        reader.BaseStream.Position = 80;
        var faceCount = Math.Min(reader.ReadUInt32(), maxFaces);

        for (var face = 0; face < faceCount; face++)
        {
            reader.ReadSingle();
            reader.ReadSingle();
            reader.ReadSingle();

            var start = vertices.Count;
            for (var i = 0; i < 3; i++)
            {
                vertices.Add(new Point3D(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                triangles.Add(start + i);
            }

            reader.ReadUInt16();
        }
    }

    private static void ReadAsciiStl(string path, List<Point3D> vertices, Int32Collection triangles)
    {
        const int maxFaces = 80_000;
        var faceVertexCount = 0;

        foreach (var rawLine in File.ReadLines(path))
        {
            if (triangles.Count / 3 >= maxFaces)
                break;

            var line = rawLine.Trim();
            if (!line.StartsWith("vertex ", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4
                || !TryParseDouble(parts[1], out var x)
                || !TryParseDouble(parts[2], out var y)
                || !TryParseDouble(parts[3], out var z))
            {
                continue;
            }

            vertices.Add(new Point3D(x, y, z));
            triangles.Add(vertices.Count - 1);
            faceVertexCount++;

            if (faceVertexCount == 3)
                faceVertexCount = 0;
        }
    }

    private static void NormalizePoints(List<Point3D> points)
    {
        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        var minZ = points.Min(p => p.Z);
        var maxX = points.Max(p => p.X);
        var maxY = points.Max(p => p.Y);
        var maxZ = points.Max(p => p.Z);
        var center = new Point3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
        var scale = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
        if (scale <= 0)
            scale = 1;

        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            points[i] = new Point3D(
                (p.X - center.X) / scale * 3,
                (p.Y - center.Y) / scale * 3,
                (p.Z - center.Z) / scale * 3);
        }
    }

    private static int ParseObjIndex(string token, int vertexCount)
    {
        var value = token.Split('/')[0];
        if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var index))
            return -1;

        return index > 0 ? index - 1 : vertexCount + index;
    }

    private static bool TryParseDouble(string value, out double result)
        => double.TryParse(
            value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out result);

    private static bool IsImageExtension(string ext)
        => new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp" }.Contains(ext);

    private void UpdatePreviewCamera()
    {
        var yaw = _previewYaw * Math.PI / 180;
        var pitch = _previewPitch * Math.PI / 180;
        var cosPitch = Math.Cos(pitch);

        var x = _previewDistance * cosPitch * Math.Sin(yaw);
        var y = _previewDistance * Math.Sin(pitch);
        var z = _previewDistance * cosPitch * Math.Cos(yaw);

        PreviewCameraPosition = new Point3D(x, y, z);
        PreviewCameraLookDirection = new Vector3D(-x, -y, -z);
    }

    private static bool IsTextLikeFile(string path, string fileName, string ext)
    {
        if (IsTextExtension(ext) || IsTextFileName(fileName))
            return true;

        return IsLikelyTextContent(path);
    }

    private static bool IsTextExtension(string ext)
        => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".log", ".md", ".json", ".xml", ".csv", ".ini", ".cfg", ".config",
            ".cs", ".xaml", ".js", ".jsx", ".ts", ".tsx", ".html", ".css", ".scss", ".sass",
            ".py", ".java", ".cpp", ".c", ".h", ".hpp", ".rs", ".go", ".php", ".rb", ".swift",
            ".sql", ".ps1", ".bat", ".cmd", ".yaml", ".yml", ".toml", ".lock", ".env",
            ".editorconfig", ".gitignore", ".gitattributes", ".dockerignore", ".npmrc"
        }.Contains(ext);

    private static bool IsTextFileName(string fileName)
    {
        var knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".env", ".gitignore", ".gitattributes", ".dockerignore", ".editorconfig",
            ".npmrc", ".yarnrc", "Dockerfile", "Makefile", "README", "LICENSE"
        };

        return knownNames.Contains(fileName)
            || fileName.StartsWith(".env.", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".env", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyTextContent(string path)
    {
        const int sampleSize = 4096;

        try
        {
            var info = new FileInfo(path);
            if (info.Length > 5_000_000)
                return false;

            using var stream = File.OpenRead(path);
            var buffer = new byte[Math.Min(sampleSize, Math.Max(0, (int)info.Length))];
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
                return true;

            var suspicious = 0;
            for (var i = 0; i < read; i++)
            {
                var b = buffer[i];
                if (b == 0)
                    return false;

                if (b < 32 && b is not 9 and not 10 and not 13)
                    suspicious++;
            }

            return (double)suspicious / read < 0.05;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsKnown3DExtension(string ext)
        => new[] { ".fbx", ".dae", ".gltf", ".glb", ".3ds", ".ply", ".blend" }.Contains(ext);

    private int BeginOperation(string message, bool indeterminate)
    {
        Cancel();
        var operationId = Interlocked.Increment(ref _activeOperationId);
        BeginBusy(message, indeterminate);
        return operationId;
    }

    private bool IsCurrentOperation(int operationId, CancellationToken ct)
        => operationId == _activeOperationId && !ct.IsCancellationRequested;

    private void BeginBusy(string message, bool indeterminate)
    {
        IsScanning = true;
        IsProgressIndeterminate = indeterminate;
        ScanProgress = 0;
        StatusMessage = message;
        OperationDetail = message;
    }

    private void EndBusy(int operationId)
    {
        if (operationId != _activeOperationId)
            return;

        IsScanning = false;
        if (!IsProgressIndeterminate && ScanProgress < 100)
            ScanProgress = 100;
    }

    private void ReportOperation(string message)
    {
        if (Settings.ShowDetailedProgress)
            OperationDetail = message;

        StatusMessage = message;
    }

    private static string Fmt(long bytes)
    {
        double size = bytes;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:F1} {units[unitIndex]}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
