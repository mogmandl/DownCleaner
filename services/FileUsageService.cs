using System.IO;
using System.Collections.Concurrent;
using Microsoft.Win32;

namespace FileCleaner.Services;

public static class FileUsageService
{
    private static readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"]  = "Adobe Acrobat / PDF Reader",
        [".docx"] = "Microsoft Word",
        [".doc"]  = "Microsoft Word",
        [".xlsx"] = "Microsoft Excel",
        [".xls"]  = "Microsoft Excel",
        [".pptx"] = "Microsoft PowerPoint",
        [".ppt"]  = "Microsoft PowerPoint",
        [".mp3"]  = "미디어 플레이어",
        [".mp4"]  = "미디어 플레이어",
        [".mkv"]  = "미디어 플레이어",
        [".avi"]  = "미디어 플레이어",
        [".jpg"]  = "사진 뷰어",
        [".jpeg"] = "사진 뷰어",
        [".png"]  = "사진 뷰어",
        [".gif"]  = "사진 뷰어",
        [".zip"]  = "압축 프로그램",
        [".rar"]  = "압축 프로그램",
        [".7z"]   = "압축 프로그램",
        [".exe"]  = "실행 파일",
        [".msi"]  = "Windows 설치 관리자",
        [".cs"]   = "Visual Studio / VSCode",
        [".py"]   = "Python",
        [".js"]   = "Node.js / 브라우저",
        [".ts"]   = "TypeScript",
        [".html"] = "웹 브라우저",
        [".css"]  = "웹 브라우저",
        [".txt"]  = "메모장",
        [".json"] = "텍스트 편집기",
        [".xml"]  = "텍스트 편집기",
        [".yaml"] = "텍스트 편집기",
        [".yml"]  = "텍스트 편집기",
        [".sql"]  = "데이터베이스 도구",
        [".psd"]  = "Adobe Photoshop",
        [".ai"]   = "Adobe Illustrator",
        [".dll"]  = "시스템 파일",
        [".sys"]  = "시스템 파일",
        [".log"]  = "로그 파일",
        [".bat"]  = "Windows 배치 파일",
        [".ps1"]  = "PowerShell",
        [".sh"]   = "Shell 스크립트",
    };

    public static string GetAssociatedProgram(string extension)
    {
        if (string.IsNullOrEmpty(extension)) return "알 수 없음";
        if (_cache.TryGetValue(extension, out var cached)) return cached;

        if (Known.TryGetValue(extension, out var known))
        {
            _cache.TryAdd(extension, known);
            return known;
        }

        string result = $"{extension} 파일";
        try
        {
            using var extKey = Registry.ClassesRoot.OpenSubKey(extension);
            var progId = extKey?.GetValue(null) as string;

            if (!string.IsNullOrEmpty(progId))
            {
                using var cmdKey = Registry.ClassesRoot
                    .OpenSubKey($@"{progId}\shell\open\command");
                var command = cmdKey?.GetValue(null) as string;

                if (!string.IsNullOrEmpty(command))
                {
                    var exe = command.Trim('"')
                        .Split(new[] { '"', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault() ?? "";
                    if (File.Exists(exe))
                        result = Path.GetFileNameWithoutExtension(exe);
                }

                if (result == $"{extension} 파일")
                {
                    using var progKey = Registry.ClassesRoot.OpenSubKey(progId);
                    var friendly = progKey?.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(friendly)) result = friendly;
                }
            }
        }
        catch { }

        _cache.TryAdd(extension, result);
        return result;
    }

    public static string GetAssociatedProgramFast(string extension)
    {
        if (string.IsNullOrEmpty(extension)) return "알 수 없음";
        if (_cache.TryGetValue(extension, out var cached)) return cached;

        if (Known.TryGetValue(extension, out var known))
        {
            _cache.TryAdd(extension, known);
            return known;
        }

        var result = $"{extension} 파일";
        _cache.TryAdd(extension, result);
        return result;
    }

    public static bool IsFileInUse(string path)
    {
        try
        {
            using var s = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException) { return true; }
        catch { return false; }
    }
}
