namespace FileCleaner.Models;

public class ErrorHistoryItem
{
    public DateTime Time { get; set; }
    public string Source { get; set; } = "";
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public string StackTrace { get; set; } = "";

    public string Summary => $"{Time:yyyy-MM-dd HH:mm} | {Source} | {Message}";
}
