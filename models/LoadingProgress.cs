namespace FileCleaner.Models;

public readonly record struct LoadingProgress(
    string Message,
    double Percent,
    bool IsIndeterminate = false);
