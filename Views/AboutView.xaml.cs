using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using ToolkitApp.Services;

namespace ToolkitApp.Views;

public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
        txtVersion.Text = "Version " + AppInfo.Version;
        txtKeys.Text =
            "Ctrl+1..4    Home / Run / Installed / GitHub\n" +
            "Ctrl+Q       GitHub search\n" +
            "Ctrl+,       Settings\n" +
            "Ctrl+F       Search tools      Ctrl+N  New tool\n" +
            "F5, Ctrl+Enter  Run            Esc     Stop / clear search\n" +
            "F2           Edit              Del     Delete\n" +
            "Double-click Run the tool";
        txtPaths.Text = $"Config: {ConfigService.ConfigPath}\nLogs:   {ConfigService.LogDirectory}\nClones: {ConfigService.ResolveToolsDirectory()}";
    }

    private void Repo_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(AppInfo.RepoUrl) { UseShellExecute = true });

    private void Logs_Click(object sender, RoutedEventArgs e)
    {
        try { System.IO.Directory.CreateDirectory(ConfigService.LogDirectory); Process.Start(new ProcessStartInfo(ConfigService.LogDirectory) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
}
