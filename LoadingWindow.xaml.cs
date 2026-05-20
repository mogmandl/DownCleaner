using System.Windows;

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
}
