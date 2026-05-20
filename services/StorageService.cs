using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using FileCleaner.Models;

namespace FileCleaner.Services;

public static class StorageService
{
    public static List<DriveItem> GetDriveInfo()
    {
        var list = new List<DriveItem>();

        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady) continue;

                list.Add(new DriveItem
                {
                    Name = string.IsNullOrWhiteSpace(d.VolumeLabel) ? d.Name : $"{d.Name} [{d.VolumeLabel}]",
                    TotalSize = d.TotalSize,
                    FreeSpace = d.AvailableFreeSpace
                });
            }
            catch
            {
                // Ignore inaccessible drives.
            }
        }

        return list.OrderBy(x => x.Name).ToList();
    }

    /// <summary>
    /// Common user folders shown in the app's quick access area.
    /// </summary>
    public static List<(string Name, string FolderPath)> GetQuickAccessFolders()
    {
        var list = new List<(string Name, string FolderPath)>
        {
            ("바탕화면", Environment.GetFolderPath(Environment.SpecialFolder.Desktop)),
            ("문서", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            ("다운로드", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")),
            ("사진", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)),
            ("음악", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)),
            ("비디오", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos))
        };

        return list
            .Where(f => !string.IsNullOrWhiteSpace(f.FolderPath) && Directory.Exists(f.FolderPath))
            .DistinctBy(f => f.FolderPath)
            .ToList();
    }

    public static async Task<List<StorageItem>> AnalyzeFolderSizesAsync(
        List<string> paths,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (paths == null || paths.Count == 0)
            return new List<StorageItem>();

        var uniquePaths = paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (uniquePaths.Count == 0)
            return new List<StorageItem>();

        progress?.Report($"저장공간 분석 시작: {uniquePaths.Count}개 폴더");

        var bag = new ConcurrentBag<StorageItem>();

        await Task.Run(() =>
        {
            var options = new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 2, 6)
            };

            Parallel.ForEach(uniquePaths, options, path =>
            {
                options.CancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"분석 중: {path}");

                var size = GetDirectorySize(path, options.CancellationToken);
                bag.Add(new StorageItem
                {
                    Name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } n ? n : path,
                    FolderPath = path,
                    Size = size
                });
            });
        }, ct);

        var items = bag.OrderByDescending(x => x.Size).ToList();
        var max = items.Count > 0 ? Math.Max(1, items.Max(x => x.Size)) : 1;

        foreach (var item in items)
            item.Percent = (double)item.Size / max * 100;

        progress?.Report($"저장공간 분석 완료: {items.Count}개 폴더");
        return items;
    }

    private static long GetDirectorySize(string rootPath, CancellationToken ct)
    {
        long total = 0;
        var dirs = new Stack<string>();
        dirs.Push(rootPath);

        while (dirs.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var current = dirs.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                try
                {
                    total += new FileInfo(file).Length;
                }
                catch
                {
                    // Ignore files that become inaccessible while scanning.
                }
            }

            IEnumerable<string> subDirs;
            try
            {
                subDirs = Directory.EnumerateDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var subDir in subDirs)
            {
                try
                {
                    var attrs = File.GetAttributes(subDir);
                    if ((attrs & FileAttributes.ReparsePoint) != 0)
                        continue; // Skip junction/symlink loops.

                    dirs.Push(subDir);
                }
                catch
                {
                    // Ignore inaccessible directories.
                }
            }
        }

        return total;
    }
}
