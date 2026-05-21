using System.Windows;
using System.Windows.Shell;
using FileCleaner.Models;

namespace FileCleaner;

public partial class LoadingWindow : Window
{
    public LoadingWindow()
    {
        InitializeComponent();
    }

    public void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    public void SetProgress(LoadingProgress progress)
    {
        StatusText.Text = progress.Message;
        LoadingProgressBar.IsIndeterminate = progress.IsIndeterminate;

        var percent = Math.Clamp(progress.Percent, 0, 100);
        LoadingProgressBar.Value = percent;

        if (TaskbarItemInfo == null)
            return;

        TaskbarItemInfo.ProgressState = progress.IsIndeterminate
            ? TaskbarItemProgressState.Indeterminate
            : TaskbarItemProgressState.Normal;
        TaskbarItemInfo.ProgressValue = percent / 100d;
    }

    public void SetError(string message)
    {
        StatusText.Text = message;
        LoadingProgressBar.IsIndeterminate = false;

        if (TaskbarItemInfo != null)
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Error;
    }

    public void SetComplete(string message)
    {
        SetProgress(new LoadingProgress(message, 100));

        if (TaskbarItemInfo != null)
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
    }
}
