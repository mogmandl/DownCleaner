using System.IO;
using System.Linq;
using System.Collections.Concurrent;
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
    private static readonly HashSet<string> ProjectScanSkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".vscode", "bin", "obj", "node_modules", "packages",
        ".next", "dist", "build", "target", "__pycache__", ".venv", "venv",
        "Library", "PackageCache", "Temp", "Logs", "UserSettings"
    };

    private static readonly HashSet<string> FileScanSkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", "node_modules", "packages", "bin", "obj", ".next",
        "dist", "build", "target", "__pycache__", ".venv", "venv",
        "Library", "PackageCache", "Temp", "Logs", "UserSettings"
    };

    private static readonly HashSet<string> DeleteCandidateExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tmp", ".temp", ".log", ".bak", ".old", ".dmp", ".cache", ".crdownload", ".part",
        ".download", ".etl", ".trace", ".swp", ".swo", ".tmp~"
    };

    private static readonly HashSet<string> ImportantExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf", ".txt", ".md",
        ".rtf", ".odt", ".ods", ".odp", ".csv",
        ".cs", ".fs", ".vb", ".java", ".kt", ".kts", ".py", ".ipynb", ".js", ".jsx",
        ".ts", ".tsx", ".vue", ".svelte", ".html", ".htm", ".css", ".scss", ".sass",
        ".less", ".cpp", ".cxx", ".cc", ".c", ".h", ".hpp", ".hh", ".go", ".rs",
        ".php", ".rb", ".swift", ".dart", ".r", ".m", ".mm", ".scala", ".sql",
        ".ps1", ".sh", ".bash", ".zsh", ".fish", ".bat", ".cmd",
        ".json", ".xml", ".yaml", ".yml", ".toml", ".ini", ".config", ".conf",
        ".csproj", ".fsproj", ".vbproj", ".vcxproj", ".sln", ".slnx", ".props", ".targets",
        ".unity", ".prefab", ".asset", ".mat", ".controller", ".anim", ".shader", ".cginc",
        ".fbx", ".obj", ".blend", ".stl", ".dae", ".gltf", ".glb",
        ".psd", ".ai", ".fig", ".sketch", ".db", ".sqlite", ".sqlite3"
    };

    private static readonly HashSet<string> ImportantFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env", ".env.local", ".env.development", ".env.production", ".gitignore", ".gitattributes",
        ".editorconfig", ".dockerignore", "Dockerfile", "docker-compose.yml", "docker-compose.yaml",
        "package.json", "package-lock.json", "pnpm-lock.yaml", "yarn.lock", "tsconfig.json",
        "jsconfig.json", "vite.config.js", "vite.config.ts", "webpack.config.js",
        "requirements.txt", "pyproject.toml", "Pipfile", "poetry.lock", "Cargo.toml",
        "Cargo.lock", "go.mod", "go.sum", "Gemfile", "Gemfile.lock", "composer.json",
        "composer.lock", "pubspec.yaml", "pubspec.lock", "CMakeLists.txt", "Makefile",
        "README", "README.md", "LICENSE", "manifest.json", "project.godot"
    };

    public static (bool IsProject, string ProjectType) DetectProjectType(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return (false, "");

        try
        {
            if (IsUnityProjectRoot(folder))
                return (true, "Unity");

            if (IsIgnoredProjectCandidate(folder))
                return (false, "");

            foreach (var (name, type) in FileMarkers)
                if (File.Exists(Path.Combine(folder, name)))
                    return (true, type);

            foreach (var (ext, type) in ExtensionMarkers)
                if (HasMatchingFiles(folder, $"*{ext}"))
                    return (true, type);

            foreach (var ext in ProjExts)
                if (HasMatchingFiles(folder, $"*{ext}"))
                    return (true, "VisualStudio");

            foreach (var (name, type) in DirectoryMarkers)
                if (Directory.Exists(Path.Combine(folder, name)))
                    return (true, type);
        }
        catch { }

        return (false, "");
    }

    private static bool IsUnityProjectRoot(string folder)
        => Directory.Exists(Path.Combine(folder, "Assets"))
            && Directory.Exists(Path.Combine(folder, "ProjectSettings"))
            && File.Exists(Path.Combine(folder, "Packages", "manifest.json"));

    private static bool IsIgnoredProjectCandidate(string folder)
    {
        var current = new DirectoryInfo(folder);
        while (current != null)
        {
            if (ProjectScanSkipDirs.Contains(current.Name))
                return true;

            current = current.Parent;
        }

        return false;
    }

    public static IEnumerable<(string FolderPath, string ProjectType)> FindProjectFolders(
        string rootPath,
        int maxDepth = 5,
        int maxFolders = 1500,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            yield break;

        var pending = new Stack<(string Path, int Depth)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormalizePath(rootPath)
        };
        var inspected = 0;

        pending.Push((rootPath, 0));
        while (pending.Count > 0 && inspected < maxFolders)
        {
            ct.ThrowIfCancellationRequested();

            var (current, depth) = pending.Pop();
            inspected++;

            var (isProject, projectType) = DetectProjectType(current);
            if (isProject)
                yield return (current, projectType);

            if (depth >= maxDepth)
                continue;

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

                var name = Path.GetFileName(child);
                if (ProjectScanSkipDirs.Contains(name))
                    continue;

                if (!ShouldVisitDirectory(child, visited))
                    continue;

                pending.Push((child, depth + 1));
            }
        }
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
        var result = new ConcurrentBag<FileItem>();

        try
        {
            await Task.Run(() =>
            {
                var count = 0;
                var options = new ParallelOptions
                {
                    CancellationToken = ct,
                    MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 2, 8)
                };

                Parallel.ForEach(EnumerateFilesSafely(folderPath, includeSubfolders, ct), options, p =>
                {
                    options.CancellationToken.ThrowIfCancellationRequested();
                    var currentCount = Interlocked.Increment(ref count);

                    try
                    {
                        var info = new FileInfo(p);
                        if (!info.Exists) return;

                        var inUse = FileUsageService.IsFileInUse(p);
                        var (riskScore, riskLevel, riskReason) = ComputeRisk(info, inUse);
                        var prog = FileUsageService.GetAssociatedProgram(info.Extension);

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
                            NeedsUsageCheck = false
                        });

                        if (currentCount % 50 == 0)
                            progress?.Report($"스캔 중... {currentCount}개 처리됨");
                    }
                    catch { }
                });
            }, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { progress?.Report($"오류: {ex.Message}"); }

        return result
            .OrderBy(file => file.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

                if (ShouldSkipFileScanDirectory(directory, rootPath))
                    continue;

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

    private static bool ShouldSkipFileScanDirectory(string path, string rootPath)
    {
        if (NormalizePath(path).Equals(NormalizePath(rootPath), StringComparison.OrdinalIgnoreCase))
            return false;

        var name = Path.GetFileName(path);
        return FileScanSkipDirs.Contains(name);
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
        var isImportant = IsImportantFile(info);
        var isDeleteCandidate = DeleteCandidateExts.Contains(ext);
        var isProtectedPath = IsProtectedSystemPath(path);

        var unusedDays = Math.Max(0, (DateTime.Now - info.LastAccessTime).TotalDays);
        if (unusedDays >= 365) { score -= 30; reasons.Add("오래 사용 안 함"); }
        else if (unusedDays >= 180) { score -= 22; reasons.Add("장기간 미사용"); }
        else if (unusedDays >= 60) { score -= 10; reasons.Add("최근 사용 이력 적음"); }
        else { score += 14; reasons.Add("최근 사용 파일"); }

        if (info.Length >= 1_000_000_000)
        {
            if (unusedDays >= 180 && !isImportant && !isProtectedPath)
            {
                score -= 12;
                reasons.Add("오래된 대용량 파일");
            }
            else
            {
                score += 18;
                reasons.Add("대용량 파일");
            }
        }
        else if (info.Length >= 300_000_000)
        {
            if (unusedDays >= 180 && !isImportant && !isProtectedPath)
            {
                score -= 6;
                reasons.Add("오래된 큰 파일");
            }
            else
            {
                score += 10;
                reasons.Add("용량 큼");
            }
        }

        if (isDeleteCandidate) { score -= 20; reasons.Add("임시/로그 계열 확장자"); }
        if (isImportant) { score += 20; reasons.Add("문서/코드/프로젝트 중요 파일"); }

        if (isProtectedPath)
        {
            score = Math.Max(score + 40, 85);
            reasons.Add("시스템 경로");
        }

        if (path.Contains("\\Users\\", StringComparison.OrdinalIgnoreCase)
            && path.Contains("\\Downloads\\", StringComparison.OrdinalIgnoreCase)
            && isDeleteCandidate)
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

        var level = FileItem.GetRiskLevel(score);

        if (reasons.Count == 0)
            reasons.Add("기본 규칙 기반 평가");

        return (score, level, string.Join(", ", reasons.Take(3)));
    }

    private static bool IsImportantFile(FileInfo info)
        => ImportantExts.Contains(info.Extension)
            || ImportantFileNames.Contains(info.Name);

    public static bool IsProtectedSystemPath(string path)
        => path.Contains("\\Windows\\", StringComparison.OrdinalIgnoreCase)
            || path.Contains("\\Program Files\\", StringComparison.OrdinalIgnoreCase)
            || path.Contains("\\Program Files (x86)\\", StringComparison.OrdinalIgnoreCase)
            || path.Contains("\\ProgramData\\", StringComparison.OrdinalIgnoreCase);
}
