using System.Runtime.InteropServices;

namespace FileCleaner.Services;

public static class RecycleBinService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPTStr)] public string  pFrom;
        [MarshalAs(UnmanagedType.LPTStr)] public string? pTo;
        public ushort fFlags;
        public bool   fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPTStr)] public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

    private const uint   FO_DELETE        = 0x0003;
    private const ushort FOF_ALLOWUNDO    = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_NOERRORUI   = 0x0400;
    private const ushort FOF_SILENT      = 0x0004;

    public static (bool Success, string Error) SendToRecycleBin(IEnumerable<string> paths)
    {
        var list = paths.ToList();
        if (!list.Any()) return (true, "");

        var pFrom = string.Join('\0', list) + "\0\0";

        var op = new SHFILEOPSTRUCT
        {
            wFunc  = FO_DELETE,
            pFrom  = pFrom,
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT
        };

        var code = SHFileOperation(ref op);
        return code == 0 ? (true, "") : (false, $"오류 코드: {code}");
    }
}
