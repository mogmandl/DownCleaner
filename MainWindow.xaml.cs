using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileCleaner.Models;
using FileCleaner.ViewModels;

namespace FileCleaner;

public partial class MainWindow : Window
{
    private MainViewModel VM => (MainViewModel)DataContext;

    public MainWindow() => InitializeComponent();

    private async void FolderTreeView_SelectedItemChanged(
        object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FolderNode node && !string.IsNullOrEmpty(node.FullPath))
        {
            VM.SetSelectedFolder(node);
            await VM.LoadFilesForFolderAsync(node);
        }
    }

    private async void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (sender is TreeViewItem { DataContext: FolderNode node })
            await VM.ExpandNodeAsync(node);
    }

    private void CurrentCleanupTreeView_SelectedItemChanged(
        object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        VM.SetSelectedCleanupNode(e.NewValue as CleanupNode);
    }

    private void CurrentCleanupTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.TreeView { SelectedItem: CleanupNode { IsFolder: false } node })
            OpenPath(node.FullPath);
    }

    private void CurrentFilesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { SelectedItem: FileItem file })
            OpenPath(file.FilePath);
    }

    private static void OpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"파일 열기 실패: {ex.Message}",
                "오류",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (sender is System.Windows.Controls.TabControl tc
                && tc.SelectedItem is System.Windows.Controls.TabItem ti
                && ti.Header is string header
                && header.Contains("저장공간"))
            {
                Debug.WriteLine("저장공간 탭 선택됨");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Tab selection error: {ex}");
        }
    }
}
