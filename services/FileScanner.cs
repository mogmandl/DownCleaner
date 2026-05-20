using System.IO;
using System.Linq;
using FileCleaner.Models;

namespace FileCleaner.Services;

public static class FileScanner
{
    private static readonly Dictionary<string, string> DirectoryMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        [".git"] = "Git",
    };

    private static readonly Dictionary<string, string> FileMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["package.json"] = "NodeJS",
        ["pom.xml"] = "Maven",
        ["Cargo.toml"] = "Rust",
        ["requirements.txt"] = "Python",
        ["setup.py"] = "Python",
        ["CMakeLists.txt"] = "CMake",
        ["Makefile"] = "Make",
        ["build.gradle"] = "Gradle",
        ["composer.json"] = "PHP",
        ["Gemfile"] = "Ruby",
        ["go.mod"] = "Go",
        ["pubspec.yaml"] = "Flutter",
    };

    private static readonly Dictionary<string, string> ExtensionMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        [".sln"] = "VisualStudio",
    };

    private static readonly string[] ProjExts = { ".csproj", ".vcxproj", ".vbproj" };
    private static readonly TimeSpan FastScanThreshold = TimeSpan.FromSeconds(2);
    private const int FastScanEvaluationStartCount = 250;

    private static readonly HashSet<string> DeleteCandidateExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tmp", ".temp", ".log", ".bak", ".old", ".dmp", ".cache", ".crdownload", ".part"
    };

    private static readonly HashSet<string> ImportantExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf", ".txt", ".md",
        ".cs", ".java", ".py", ".js", ".ts", ".cpp", ".h", ".sql", ".ps1", ".csproj", ".sln"
    };

    public static (bool IsProject, string ProjectType) DetectProjectType(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return (false, "");

        try
        {
            foreach (var (name, type) in DirectoryMarkers)
                if (Directory.Exists(Path.Combine(folder, name)))
                    return (true, type);

            foreach (var (name, type) in FileMarkers)
                if (File.Exists(Path.Combine(folder, name)))
                    return (true, type);

            foreach (var (ext, type) in ExtensionMarkers)
                if (HasMatchingFiles(folder, $"*{ext}"))
                    return (true, type);

            foreach (var ext in ProjExts)
                if (HasMatchingFiles(folder, $"*{ext}"))
                    return (true, "VisualStudio");
        }
        catch { }

        return (false, "");
    }

    public static async Task LoadChildrenAsync(
        FolderNode node,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        node.IsLoading = true;

        var dummy = node.Children.FirstOrDefault(c => string.IsNullOrEmpty(c.FullPath));
        if (dummy != null) node.Children.Remove(dummy);

        try
        {
            var dirs = await Task.Run(
                () => EnumerateDirectoriesSafely(node.FullPath, recursive: false, ct).ToArray(),
                ct);

            foreach (var dir in dirs)
            {
                ct.ThrowIfCancellationRequested();

                var (isProj, projType) = await Task.Run(() => DetectProjectType(dir), ct);
                var child = new FolderNode
                {
                    Name = Path.GetFileName(dir) ?? dir,
                    FullPath = dir,
                    IsProjectFolder = isProj,
                    ProjectType = projType
                };

                if (!isProj)
                {
                    var hasSub = await Task.Run(() =>
                    {
                        return EnumerateDirectoriesSafely(dir, recursive: false, ct).Any();
                    }, ct);

                    if (hasSub)
                        child.Children.Add(new FolderNode { Name = "...", FullPath = "" });
                }

                node.Children.Add(child);
                progress?.Report($"로드: {child.Name}");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { }
        finally { node.IsLoading = false; }
    }

    public static async Task<List<FileItem>> ScanFilesAsync(
        string folderPath,
        bool includeSubfolders,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var result = new List<FileItem>();

        try
        {
            await Task.Run(() =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var count = 0;
                var isFastMode = false;

                foreach (var p in EnumerateFilesSafely(folderPath, includeSubfolders, ct))
                {
                    ct.ThrowIfCancellationRequested();
                    count++;

                    try
                    {
                        var info = new FileInfo(p);
                        if (!info.Exists) continue;

                        if (!isFastMode
                            && count >= FastScanEvaluationStartCount
                            && stopwatch.Elapsed >= FastScanThreshold)
                        {
                            isFastMode = true;
                            progress?.Report("빠른 스캔 모드: 무거운 파일 점유 검사를 뒤로 미룹니다");
                        }

                        var deferUsageCheck = isFastMode;
                        var inUse = deferUsageCheck ? false : FileUsageService.IsFileInUse(p);
                        var (riskScore, riskLevel, riskReason) = ComputeRisk(info, inUse);
                        var prog = deferUsageCheck
                            ? FileUsageService.GetAssociatedProgramFast(info.Extension)
                            : FileUsageService.GetAssociatedProgram(info.Extension);

                        if (deferUsageCheck)
                            riskReason = AppendReason(riskReason, "빠른 스캔: 사용 여부 미확인");

                        result.Add(new FileItem
                        {
                            FileName = info.Name,
                            FilePath = info.FullName,
                            FileSize = info.Length,
                            LastModified = info.LastWriteTime,
                            LastAccessed = info.LastAccessTime,
                            RiskScore = riskScore,
                            RiskLevel = riskLevel,
                            RiskReason = riskReason,
                            AssociatedProgram = prog,
                            IsInUse = inUse,
                            NeedsUsageCheck = deferUsageCheck
                        });

                        if (count % 50 == 0)
                            progress?.Report($"스캔 중... {count}개 처리됨");
                    }
                    catch { }
                }
            }, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { progress?.Report($"오류: {ex.Message}"); }

        return result;
    }

    public static async Task<List<FileItem>> ScanSmartForCleanupAsync(
        string rootPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var merged = new List<FileItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            return merged;

        var (isRootProject, rootType) = DetectProjectType(rootPath);
        if (isRootProject)
        {
            progress?.Report($"프로젝트 폴더 감지: {Path.GetFileName(rootPath)} [{rootType}]");
            var files = await ScanFilesAsync(rootPath, includeSubfolders: true, progress, ct);
            foreach (var file in files)
            {
                if (seen.Add(file.FilePath))
                    merged.Add(file);
            }

            return merged;
        }

        progress?.Report($"일반 폴더 감지: {Path.GetFileName(rootPath)}");
        var rootFiles = await ScanFilesAsync(rootPath, includeSubfolders: false, progress, ct);
        foreach (var file in rootFiles)
        {
            if (seen.Add(file.FilePath))
                merged.Add(file);
        }

        var childDirs = EnumerateDirectoriesSafely(rootPath, recursive: false, ct).ToArray();

        foreach (var dir in childDirs)
        {
            ct.ThrowIfCancellationRequested();

            var (isProject, projectType) = DetectProjectType(dir);
            if (isProject)
            {
                progress?.Report($"하위 프로젝트 감지: {Path.GetFileName(dir)} [{projectType}]");
                var projectFiles = await ScanFilesAsync(dir, includeSubfolders: true, progress, ct);
                foreach (var file in projectFiles)
                {
                    if (seen.Add(file.FilePath))
                        merged.Add(file);
                }
            }
            else
            {
                var bundleFiles = await ScanFilesAsync(dir, includeSubfolders: false, progress, ct);
                foreach (var file in bundleFiles)
                {
                    if (seen.Add(file.FilePath))
                        merged.Add(file);
                }
            }
        }

        return merged;
    }

    public static bool TryResolveDeferredMetadata(
        string path,
        string existingAssociatedProgram,
        out (bool IsInUse, int RiskScore, string RiskLevel, string RiskReason, string AssociatedProgram) metadata)
    {
        metadata = default;

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                return false;

            var inUse = FileUsageService.IsFileInUse(info.FullName);
            var (riskScore, riskLevel, riskReason) = ComputeRisk(info, inUse);
            var associatedProgram = string.IsNullOrWhiteSpace(existingAssociatedProgram)
                ? FileUsageService.GetAssociatedProgram(info.Extension)
                : existingAssociatedProgram;

            metadata = (inUse, riskScore, riskLevel, riskReason, associatedProgram);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static IEnumerable<string> EnumerateDirectoriesSafely(
        string rootPath,
        bool recursive,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            yield break;

        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormalizePath(rootPath)
        };

        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var current = pending.Pop();
            string[] children;

            try
            {
                children = Directory.GetDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var child in children)
            {
                ct.ThrowIfCancellationRequested();

                if (!ShouldVisitDirectory(child, visited))
                    continue;

                yield return child;

                if (recursive)
                    pending.Push(child);
            }
        }
    }

    private static IEnumerable<string> EnumerateFilesSafely(
        string rootPath,
        bool includeSubfolders,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            yield break;

        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormalizePath(rootPath)
        };

        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var current = pending.Pop();
            string[] files;

            try
            {
                files = Directory.GetFiles(current);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                yield return file;
            }

            if (!includeSubfolders)
                continue;

            string[] directories;

            try
            {
                directories = Directory.GetDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var directory in directories)
            {
                ct.ThrowIfCancellationRequested();

                if (!ShouldVisitDirectory(directory, visited))
                    continue;

                pending.Push(directory);
            }
        }
    }

    private static bool ShouldVisitDirectory(string path, ISet<string> visited)
    {
        try
        {
            var attrs = File.GetAttributes(path);
            if ((attrs & FileAttributes.ReparsePoint) != 0)
                return false;
        }
        catch
        {
            return false;
        }

        return visited.Add(NormalizePath(path));
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    private static bool HasMatchingFiles(string folder, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(folder, pattern, SearchOption.TopDirectoryOnly).Any();
        }
        catch
        {
            return false;
        }
    }

    private static string AppendReason(string riskReason, string extraReason)
    {
        if (string.IsNullOrWhiteSpace(riskReason))
            return extraReason;

        return $"{riskReason}, {extraReason}";
    }

    private static (int Score, string Level, string Reason) ComputeRisk(FileInfo info, bool isInUse)
    {
        var score = 50;
        var reasons = new List<string>();
        var ext = info.Extension;
        var path = info.FullName;

        var unusedDays = Math.Max(0, (DateTime.Now - info.LastAccessTime).TotalDays);
        if (unusedDays >= 365) { score -= 30; reasons.Add("오래 사용 안 함"); }
        else if (unusedDays >= 180) { score -= 22; reasons.Add("장기간 미사용"); }
        else if (unusedDays >= 60) { score -= 10; reasons.Add("최근 사용 이력 적음"); }
        else { score += 14; reasons.Add("최근 사용 파일"); }

        if (info.Length >= 1_000_000_000) { score += 18; reasons.Add("대용량 파일"); }
        else if (info.Length >= 300_000_000) { score += 10; reasons.Add("용량 큼"); }

        if (DeleteCandidateExts.Contains(ext)) { score -= 20; reasons.Add("임시/로그 계열 확장자"); }
        if (ImportantExts.Contains(ext)) { score += 20; reasons.Add("문서/코드 중요 확장자"); }

        if (path.Contains("\\Windows\\", StringComparison.OrdinalIgnoreCase)
            || path.Contains("\\Program Files", StringComparison.OrdinalIgnoreCase))
        {
            score += 30;
            reasons.Add("시스템 경로");
        }

        if (path.Contains("\\Users\\", StringComparison.OrdinalIgnoreCase)
            && path.Contains("\\Downloads\\", StringComparison.OrdinalIgnoreCase)
            && DeleteCandidateExts.Contains(ext))
        {
            score -= 8;
            reasons.Add("다운로드 폴더 임시파일");
        }

        if (isInUse)
        {
            score += 25;
            reasons.Add("현재 사용 중");
        }

        score = Math.Clamp(score, 0, 100);

        var level = score switch
        {
            >= 70 => "높음 (삭제 주의)",
            >= 40 => "중간",
            _ => "낮음 (삭제 후보)"
        };

        if (reasons.Count == 0)
            reasons.Add("기본 규칙 기반 평가");

        return (score, level, string.Join(", ", reasons.Take(3)));
    }
}
