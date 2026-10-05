using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ToolkitApp.Models;
using ToolkitApp.Services;
using ToolkitApp.Views;

namespace ToolkitApp;

public partial class MainWindow : Window
{
    public static MainWindow? Instance { get; private set; }

    private readonly Dictionary<string, UserControl> _views = new();
    private static readonly Dictionary<string, string> Titles = new()
    {
        ["Home"] = "Home", ["Tools"] = "Run Tools", ["Installed"] = "Installed Tools",
        ["Github"] = "GitHub Extension Market", ["Settings"] = "Settings", ["About"] = "About"
    };

    public MainWindow()
    {
        InitializeComponent();
        Instance = this;
        ThemeService.Attach(this);

        var s = ConfigService.Settings;
        Width = Math.Max(MinWidth, s.WindowWidth);
        Height = Math.Max(MinHeight, s.WindowHeight);
        if (s.WindowLeft is double l && s.WindowTop is double t && IsVisibleOnScreen(l, t, Width, Height))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = l; Top = t;
        }
        if (s.WindowMaximized) WindowState = WindowState.Maximized;

        txtVersion.Text = "v" + AppInfo.Version;
        txtConfigPath.Text = ConfigService.ConfigPath;
        Navigate(s.StartView is "Home" or "Tools" or "Installed" or "Github" ? s.StartView : "Home");
        if (ConfigService.LoadWarning != null) SetStatus(ConfigService.LoadWarning);
    }

    private static bool IsVisibleOnScreen(double l, double t, double w, double h) =>
        l + w > SystemParameters.VirtualScreenLeft + 80 && t + 40 > SystemParameters.VirtualScreenTop &&
        l < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 80 &&
        t < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 80;

    public void SetStatus(string text) => txtStatus.Text = text;

    public void Navigate(string key)
    {
        if (!_views.TryGetValue(key, out var view))
        {
            view = key switch
            {
                "Home" => new HomeView(),
                "Tools" => new ToolsView(),
                "Installed" => new InstalledToolsView(),
                "Github" => new GithubInstallView(),
                "Settings" => new SettingsView(),
                _ => new AboutView()
            };
            _views[key] = view;
        }
        MainContent.Content = view;
        txtPageTitle.Text = Titles.GetValueOrDefault(key, key);
        foreach (var rb in NavPanel.Children.OfType<RadioButton>())
            rb.IsChecked = (rb.Tag as string) == key;
    }

    /// <summary>Opens the Run Tools page with this tool selected, optionally starting it.</summary>
    public void OpenTool(ToolItem tool, bool run)
    {
        Navigate("Tools");
        if (_views["Tools"] is ToolsView tv) tv.SelectTool(tool, run);
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string key) Navigate(key);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        string? target = e.Key switch
        {
            Key.Q => "Github",
            Key.OemComma => "Settings",
            Key.D1 => "Home",
            Key.D2 => "Tools",
            Key.D3 => "Installed",
            Key.D4 => "Github",
            _ => null
        };
        if (target != null) { Navigate(target); e.Handled = true; }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_views.TryGetValue("Tools", out var v) && v is ToolsView tv && tv.IsRunning)
        {
            var r = MessageBox.Show("A command is still running. Stop it and exit?", "Tool Kit",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) { e.Cancel = true; return; }
            tv.StopRunning();
        }
        var s = ConfigService.Settings;
        var b = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        s.WindowWidth = b.Width; s.WindowHeight = b.Height; s.WindowLeft = b.Left; s.WindowTop = b.Top;
        s.WindowMaximized = WindowState == WindowState.Maximized;
        ConfigService.Save();
    }
}
