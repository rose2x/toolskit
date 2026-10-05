using System.Windows;
using System.Windows.Threading;
using ToolkitApp.Services;

namespace ToolkitApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnUiException;
        AppDomain.CurrentDomain.UnhandledException += (_, a) => ConfigService.Log("Fatal", a.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, a) => { ConfigService.Log("Unobserved task", a.Exception); a.SetObserved(); };

        ConfigService.Load();
        ThemeService.Apply(ConfigService.Settings);
        new MainWindow().Show();
    }

    private void OnUiException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ConfigService.Log("UI exception", e.Exception);
        MessageBox.Show($"{e.Exception.Message}\n\nDetails were written to:\n{ConfigService.LogDirectory}",
            "Tool Kit – unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
