using System;
using System.Windows;
using System.Windows.Threading;
using FileCleaner.Services;
using FileCleaner.ViewModels;

namespace FileCleaner;

public partial class App : System.Windows.Application
{
    private bool _hasShownError;

    public App()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var loadingWindow = new LoadingWindow();
        loadingWindow.Show();

        var viewModel = new MainViewModel();
        try
        {
            var progress = new Progress<string>(loadingWindow.SetStatus);
            await viewModel.PreloadFavoriteFoldersAsync(progress);
        }
        catch (Exception ex)
        {
            ErrorLogService.LogException("Startup Preload", ex);
            loadingWindow.SetStatus("사전 분석 중 오류가 발생했습니다. 기본 화면을 엽니다.");
            await System.Threading.Tasks.Task.Delay(600);
        }

        var mainWindow = new MainWindow(viewModel);
        MainWindow = mainWindow;
        mainWindow.Show();
        loadingWindow.Close();
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (_hasShownError) return;

        _hasShownError = true;
        ErrorLogService.LogException("UI Thread", e.Exception);

        var errorMessage =
            $"UI 스레드 예외 발생:\n{e.Exception.Message}\n\n스택 트레이스:\n{e.Exception.StackTrace}\n\n" +
            $"로그 파일: {AppDataPaths.ErrorLogPath}";

        System.Windows.MessageBox.Show(errorMessage, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        Shutdown();
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (_hasShownError) return;

        _hasShownError = true;
        if (e.ExceptionObject is Exception ex)
        {
            ErrorLogService.LogException("Non-UI Thread", ex);
            var errorMessage =
                $"비UI 스레드 예외 발생:\n{ex.Message}\n\n스택 트레이스:\n{ex.StackTrace}\n\n" +
                $"로그 파일: {AppDataPaths.ErrorLogPath}";

            System.Windows.MessageBox.Show(errorMessage, "치명적 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        Shutdown();
    }
}

