using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ToolkitApp.Models;
using ToolkitApp.Services;

namespace ToolkitApp.Views;

public partial class InstalledToolsView : UserControl
{
    private readonly ICollectionView _view;

    public InstalledToolsView()
    {
        InitializeComponent();
        _view = new CollectionViewSource { Source = ConfigService.Current.Tools }.View;
        _view.SortDescriptions.Add(new SortDescription(nameof(ToolItem.Favorite), ListSortDirection.Descending));
        _view.SortDescriptions.Add(new SortDescription(nameof(ToolItem.Name), ListSortDirection.Ascending));
        _view.Filter = o =>
        {
            if (o is not ToolItem t) return false;
            var q = txtSearch.Text.Trim();
            return q.Length == 0 || t.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || t.Command.Contains(q, StringComparison.OrdinalIgnoreCase)
                   || t.Category.Contains(q, StringComparison.OrdinalIgnoreCase);
        };
        lstCards.ItemsSource = _view;
        ConfigService.Current.Tools.CollectionChanged += (_, _) => UpdateCount();
        Loaded += (_, _) => { _view.Refresh(); UpdateCount(); };
    }

    private void UpdateCount() => lblCount.Text = $"{ConfigService.Current.Tools.Count} tools";

    private void TxtSearch_Changed(object sender, TextChangedEventArgs e)
    {
        lblHint.Visibility = txtSearch.Text.Length == 0 ? Visibility.Visible : Visibility.Hidden;
        _view.Refresh();
    }

    private static ToolItem? Tool(object sender) => (sender as FrameworkElement)?.DataContext as ToolItem;

    private void Run_Click(object sender, RoutedEventArgs e) { if (Tool(sender) is ToolItem t) MainWindow.Instance?.OpenTool(t, run: true); }

    private void Fav_Click(object sender, RoutedEventArgs e)
    {
        if (Tool(sender) is not ToolItem t) return;
        t.Favorite = !t.Favorite;
        ConfigService.Save();
        _view.Refresh();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (Tool(sender) is not ToolItem t) return;
        var win = new ToolEditorWindow(t, isNew: false) { Owner = Window.GetWindow(this) };
        if (win.ShowDialog() != true) return;
        t.CopyFrom(win.Result);
        ConfigService.Save();
        _view.Refresh();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Tool(sender) is not ToolItem t) return;
        if (ConfigService.Settings.ConfirmDelete &&
            MessageBox.Show(Window.GetWindow(this), $"Delete “{t.Name}”?", "Delete tool", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        ConfigService.Current.Tools.Remove(t);
        ConfigService.Save();
    }
}
