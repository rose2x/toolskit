using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ToolkitApp.Models;
using ToolkitApp.Services;

namespace ToolkitApp.Views;

public partial class SettingsView : UserControl
{
    private static readonly (string Name, string Bg, string Fg)[] Schemes =
    {
        ("Classic", "#0C0C0C", "#D4D4D4"),
        ("Green phosphor", "#0C0C0C", "#00FF00"),
        ("Solarized dark", "#002B36", "#93A1A1"),
        ("Monokai", "#272822", "#F8F8F2"),
        ("Light paper", "#FFFFFF", "#1E1E1E"),
        ("Custom", "", "")
    };
    private static readonly string[] Accents = { "#007ACC", "#0E9F6E", "#8B5CF6", "#E11D48", "#F59E0B", "#14B8A6", "#EC4899" };
    private bool _loading;

    public SettingsView()
    {
        InitializeComponent();
        cmbTheme.ItemsSource = new[] { "Dark", "Light" };
        cmbUiFont.ItemsSource = new[] { 12, 13, 14, 15, 16, 18 };
        cmbTermSize.ItemsSource = new[] { 10, 11, 12, 13, 14, 16, 18, 20 };
        cmbTermFont.ItemsSource = new[] { "Consolas", "Cascadia Mono", "Cascadia Code", "Courier New", "Lucida Console", "JetBrains Mono" };
        cmbScheme.ItemsSource = Schemes.Select(s => s.Name).ToList();
        cmbShell.ItemsSource = ToolItem.Shells;
        cmbStart.ItemsSource = new[] { "Home", "Tools", "Installed", "Github" };
        cmbMaxLines.ItemsSource = new[] { 1000, 5000, 20000, 50000 };
        cmbPage.ItemsSource = new[] { 20, 30, 50, 100 };

        foreach (var hex in Accents)
        {
            var b = new Button
            {
                Width = 24, Height = 24, Padding = new Thickness(0), Margin = new Thickness(0, 0, 6, 0), ToolTip = hex,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex))
            };
            b.Click += (_, _) => txtAccent.Text = hex;
            pnlSwatches.Children.Add(b);
        }
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e) => LoadFromSettings();

    private void LoadFromSettings()
    {
        _loading = true;
        var s = ConfigService.Settings;
        cmbTheme.SelectedItem = s.Theme;
        cmbUiFont.SelectedItem = (int)Math.Round(s.UiFontSize);
        txtAccent.Text = s.AccentColor;
        cmbTermFont.Text = s.TerminalFontFamily;
        cmbTermSize.SelectedItem = (int)Math.Round(s.TerminalFontSize);
        txtTermBg.Text = s.TerminalBackground;
        txtTermFg.Text = s.TerminalForeground;
        var scheme = Schemes.FirstOrDefault(x => x.Bg.Equals(s.TerminalBackground, StringComparison.OrdinalIgnoreCase) && x.Fg.Equals(s.TerminalForeground, StringComparison.OrdinalIgnoreCase));
        cmbScheme.SelectedItem = scheme.Name ?? "Custom";
        cmbShell.SelectedItem = s.DefaultShell;
        cmbStart.SelectedItem = s.StartView;
        cmbMaxLines.SelectedItem = s.MaxOutputLines;
        cmbPage.SelectedItem = s.GithubPageSize;
        chkClear.IsChecked = s.ClearOutputOnRun;
        chkAutoScroll.IsChecked = s.AutoScroll;
        chkWrap.IsChecked = s.WrapOutput;
        chkTime.IsChecked = s.ShowTimestamps;
        chkConfirmAdmin.IsChecked = s.ConfirmAdmin;
        chkConfirmDelete.IsChecked = s.ConfirmDelete;
        pwdToken.Password = s.GithubToken;
        txtToolsDir.Text = s.ToolsDirectory;
        lblPath.Text = "Config file: " + ConfigService.ConfigPath;
        lblSaved.Text = "";
        _loading = false;
        UpdateAccentPreview();
    }

    private void UpdateAccentPreview()
    {
        accentPreview.Background = ThemeService.TryParseColor(txtAccent.Text, out var c) ? new SolidColorBrush(c) : Brushes.Transparent;
    }

    private void Accent_Changed(object sender, TextChangedEventArgs e) { if (!_loading) UpdateAccentPreview(); }

    private void Scheme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || cmbScheme.SelectedItem is not string name || name == "Custom") return;
        var sc = Schemes.First(x => x.Name == name);
        txtTermBg.Text = sc.Bg;
        txtTermFg.Text = sc.Fg;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!ThemeService.TryParseColor(txtAccent.Text, out _)) { Warn("Accent color is not a valid color (use a hex value like #007ACC)."); return; }
        if (!ThemeService.TryParseColor(txtTermBg.Text, out _) || !ThemeService.TryParseColor(txtTermFg.Text, out _)) { Warn("Terminal colors must be valid colors like #0C0C0C."); return; }

        var s = ConfigService.Settings;
        s.Theme = cmbTheme.SelectedItem as string ?? "Dark";
        s.UiFontSize = cmbUiFont.SelectedItem is int ui ? ui : 14;
        s.AccentColor = txtAccent.Text.Trim();
        s.TerminalFontFamily = string.IsNullOrWhiteSpace(cmbTermFont.Text) ? "Consolas" : cmbTermFont.Text.Trim();
        s.TerminalFontSize = cmbTermSize.SelectedItem is int ts ? ts : 13;
        s.TerminalBackground = txtTermBg.Text.Trim();
        s.TerminalForeground = txtTermFg.Text.Trim();
        s.DefaultShell = cmbShell.SelectedItem as string ?? "cmd";
        s.StartView = cmbStart.SelectedItem as string ?? "Home";
        s.MaxOutputLines = cmbMaxLines.SelectedItem is int ml ? ml : 5000;
        s.GithubPageSize = cmbPage.SelectedItem is int pg ? pg : 30;
        s.ClearOutputOnRun = chkClear.IsChecked == true;
        s.AutoScroll = chkAutoScroll.IsChecked == true;
        s.WrapOutput = chkWrap.IsChecked == true;
        s.ShowTimestamps = chkTime.IsChecked == true;
        s.ConfirmAdmin = chkConfirmAdmin.IsChecked == true;
        s.ConfirmDelete = chkConfirmDelete.IsChecked == true;
        s.GithubToken = pwdToken.Password.Trim();
        s.ToolsDirectory = txtToolsDir.Text.Trim();

        ThemeService.Apply(s);
        ConfigService.NotifyChanged();
        lblSaved.Text = ConfigService.Save() ? "✔ Saved" : "Could not save: " + ConfigService.LastError;
    }

    private void Warn(string msg) => MessageBox.Show(Window.GetWindow(this), msg, "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);

    private void Revert_Click(object sender, RoutedEventArgs e) => LoadFromSettings();

    private void BrowseTools_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Folder for cloned tools" };
        if (dlg.ShowDialog(Window.GetWindow(this)) == true) txtToolsDir.Text = dlg.FolderName;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "ToolKit tools (*.json)|*.json", FileName = "toolkit-tools.json" };
        if (dlg.ShowDialog() != true) return;
        try { ConfigService.Export(dlg.FileName); lblSaved.Text = $"✔ Exported {ConfigService.Current.Tools.Count} tools"; }
        catch (Exception ex) { Warn("Export failed: " + ex.Message); }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "ToolKit tools (*.json)|*.json|All files|*.*" };
        if (dlg.ShowDialog() != true) return;
        var r = MessageBox.Show(Window.GetWindow(this), "Merge with your existing tools?\n\nYes = merge (duplicates are skipped)\nNo = replace everything\nCancel = abort",
            "Import tools", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (r == MessageBoxResult.Cancel) return;
        try { int n = ConfigService.Import(dlg.FileName, replace: r == MessageBoxResult.No); lblSaved.Text = $"✔ Imported {n} tools"; }
        catch (Exception ex) { Warn("Import failed: " + ex.Message); }
    }

    private void Starter_Click(object sender, RoutedEventArgs e)
    {
        int n = ConfigService.AddStarterPack();
        ConfigService.Save();
        lblSaved.Text = n == 0 ? "Starter tools are already in your library." : $"✔ Added {n} starter tools";
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(ConfigService.ConfigDirectory) { UseShellExecute = true }); }
        catch (Exception ex) { Warn(ex.Message); }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Window.GetWindow(this), "Reset all settings to defaults? Your tools are kept.", "Reset settings",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var fresh = new AppSettings { StarterPackOffered = true };
        ConfigService.Current.Settings = fresh;
        ThemeService.Apply(fresh);
        ConfigService.NotifyChanged();
        ConfigService.Save();
        LoadFromSettings();
        lblSaved.Text = "✔ Settings reset";
    }
}
