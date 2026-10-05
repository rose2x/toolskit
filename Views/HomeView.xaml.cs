using System.Windows;
using System.Windows.Controls;
using ToolkitApp.Models;
using ToolkitApp.Services;

namespace ToolkitApp.Views;

public partial class HomeView : UserControl
{
    public HomeView() { InitializeComponent(); }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        var tools = ConfigService.Current.Tools;
        var fav = tools.Where(t => t.Favorite).OrderBy(t => t.Name).ToList();
        var recent = tools.Where(t => t.LastRun != null).OrderByDescending(t => t.LastRun).Take(6).ToList();
        lstFav.ItemsSource = fav;
        lstRecent.ItemsSource = recent;
        txtNoFav.Visibility = fav.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        txtNoRecent.Visibility = recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        txtSummary.Text = $"{tools.Count} tools · {fav.Count} favorites · {tools.Select(t => t.Category).Distinct().Count()} categories";
    }

    private void Quick_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string key }) MainWindow.Instance?.Navigate(key);
    }

    private void Tool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ToolItem t }) MainWindow.Instance?.OpenTool(t, run: false);
    }

    private void NewTool_Click(object sender, RoutedEventArgs e)
    {
        var win = new ToolEditorWindow(new ToolItem { Shell = ConfigService.Settings.DefaultShell }, isNew: true) { Owner = Window.GetWindow(this) };
        if (win.ShowDialog() == true)
        {
            ConfigService.Current.Tools.Add(win.Result);
            ConfigService.Save();
            UserControl_Loaded(this, new RoutedEventArgs());
        }
    }
}
