using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using ToolkitApp.Models;
using ToolkitApp.Services;

namespace ToolkitApp.Views;

public partial class ToolsView : UserControl
{
    private const string AllCategories = "All categories";
    private const string FavoritesOnly = "★ Favorites";

    private readonly ICollectionView _view;
    private readonly CommandRunner _runner = new();
    private readonly ConcurrentQueue<(string Text, OutputKind Kind)> _queue = new();
    private readonly DispatcherTimer _timer;
    private readonly Paragraph _para = new() { Margin = new Thickness(0) };
    private int _lineCount;
    private string _category = AllCategories;
    private bool _rebuilding, _ready;
    private DateTime _started;

    public bool IsRunning => _runner.IsRunning;

    public ToolsView()
    {
        InitializeComponent();

        txtTerminal.Document = new FlowDocument(_para) { PagePadding = new Thickness(0), Background = System.Windows.Media.Brushes.Transparent };

        _view = new CollectionViewSource { Source = ConfigService.Current.Tools }.View;
        _view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ToolItem.Category)));
        _view.SortDescriptions.Add(new SortDescription(nameof(ToolItem.Category), ListSortDirection.Ascending));
        _view.SortDescriptions.Add(new SortDescription(nameof(ToolItem.Favorite), ListSortDirection.Descending));
        _view.SortDescriptions.Add(new SortDescription(nameof(ToolItem.Name), ListSortDirection.Ascending));
        _view.Filter = FilterTool;
        lstTools.ItemsSource = _view;

        ConfigService.Current.Tools.CollectionChanged += (_, _) => { RebuildCategories(); UpdateEmpty(); };
        ConfigService.Changed += ApplyTerminalSettings;
        _runner.Output += (text, kind) => _queue.Enqueue((text, kind));

        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(60) };
        _timer.Tick += (_, _) => { FlushQueue(); if (IsRunning) lblElapsed.Text = (DateTime.Now - _started).ToString(@"mm\:ss"); };
        _timer.Start();

        chkWrap.IsChecked = ConfigService.Settings.WrapOutput;
        chkScroll.IsChecked = ConfigService.Settings.AutoScroll;
        _ready = true;
        ApplyTerminalSettings();
        RebuildCategories();
        UpdateEmpty();
        UpdateButtons();
    }

    // ---------- list / filter ----------
    private bool FilterTool(object o)
    {
        if (o is not ToolItem t) return false;
        if (_category == FavoritesOnly) { if (!t.Favorite) return false; }
        else if (_category != AllCategories && !t.Category.Equals(_category, StringComparison.OrdinalIgnoreCase)) return false;

        var q = txtSearch?.Text.Trim() ?? "";
        if (q.Length == 0) return true;
        foreach (var term in q.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            bool hit = t.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || t.Command.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || t.Category.Contains(term, StringComparison.OrdinalIgnoreCase) || t.Description.Contains(term, StringComparison.OrdinalIgnoreCase);
            if (!hit) return false;
        }
        return true;
    }

    private void RebuildCategories()
    {
        _rebuilding = true;
        var items = new List<string> { AllCategories, FavoritesOnly };
        items.AddRange(ConfigService.Current.Tools.Select(t => t.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c));
        cmbCategory.ItemsSource = items;
        cmbCategory.SelectedItem = items.Contains(_category) ? _category : AllCategories;
        _category = (string)cmbCategory.SelectedItem;
        _rebuilding = false;
        _view.Refresh();
    }

    private void UpdateEmpty() =>
        lblEmpty.Visibility = _view.IsEmpty ? Visibility.Visible : Visibility.Collapsed;

    private void CmbCategory_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_rebuilding || cmbCategory.SelectedItem is not string s) return;
        _category = s;
        _view.Refresh();
        UpdateEmpty();
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        lblSearchHint.Visibility = txtSearch.Text.Length == 0 ? Visibility.Visible : Visibility.Hidden;
        _view.Refresh();
        UpdateEmpty();
    }

    private ToolItem? Selected => lstTools.SelectedItem as ToolItem;

    private void LstTools_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateButtons();
        lblTitle.Text = Selected != null ? $"Terminal output — {Selected.Name}" : "Terminal output";
    }

    private void UpdateButtons()
    {
        bool sel = Selected != null, run = IsRunning;
        btnRun.IsEnabled = sel && !run;
        btnRunAdmin.IsEnabled = sel && !run;
        btnStop.IsEnabled = run;
        btnEdit.IsEnabled = btnDuplicate.IsEnabled = btnFav.IsEnabled = btnDelete.IsEnabled = sel;
    }

    public void SelectTool(ToolItem tool, bool run)
    {
        txtSearch.Text = "";
        _category = AllCategories;
        RebuildCategories();
        lstTools.SelectedItem = tool;
        lstTools.ScrollIntoView(tool);
        lstTools.Focus();
        if (run) _ = RunSelectedAsync(false);
    }

    // ---------- run ----------
    private void BtnRun_Click(object sender, RoutedEventArgs e) => _ = RunSelectedAsync(false);
    private void BtnRunAdmin_Click(object sender, RoutedEventArgs e) => _ = RunSelectedAsync(true);
    private void BtnStop_Click(object sender, RoutedEventArgs e) => StopRunning();
    private void LstTools_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && ItemsControl.ContainerFromElement(lstTools, d) is ListBoxItem) _ = RunSelectedAsync(false);
    }

    public void StopRunning() => _runner.Stop();

    private async Task RunSelectedAsync(bool forceAdmin)
    {
        if (IsRunning) return;
        if (Selected is not ToolItem tool) { MainWindow.Instance?.SetStatus("Select a tool first."); return; }
        var s = ConfigService.Settings;

        string command = tool.Command;
        var parameters = PlaceholderResolver.Find(command);
        if (parameters.Count > 0)
        {
            var values = ParameterDialog.Ask(Window.GetWindow(this), tool.Name, parameters);
            if (values == null) return;
            command = PlaceholderResolver.Apply(command, values);
        }

        bool admin = forceAdmin || tool.RunAsAdmin;
        if (admin && s.ConfirmAdmin &&
            MessageBox.Show(Window.GetWindow(this), $"“{tool.Name}” will run with administrator rights.\n\n{command}\n\nContinue?",
                "Run as administrator", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        if (s.ClearOutputOnRun) ClearTerminal();
        AppendLine($"▶ {tool.Name}  [{tool.Shell}{(admin ? ", admin" : "")}]", OutputKind.Info);
        AppendLine("$ " + command, OutputKind.Info);

        tool.LastRun = DateTime.Now;
        tool.RunCount++;
        ConfigService.Save();

        _started = DateTime.Now;
        SetRunning(true);
        lblResult.Text = "Running…";
        var result = await _runner.RunAsync(tool, command, admin);
        FlushQueue();

        string secs = result.Elapsed.TotalSeconds.ToString("0.0") + "s";
        if (result.Error != null) { AppendLine("✖ " + result.Error, OutputKind.Stderr); lblResult.Text = "Failed to start"; }
        else if (result.Launched) { lblResult.Text = "Launched in elevated window"; }
        else if (result.TimedOut) { AppendLine($"⏱ Timed out after {tool.TimeoutSeconds}s and was stopped.", OutputKind.Stderr); lblResult.Text = "Timed out"; }
        else if (result.Cancelled) { AppendLine("■ Stopped.", OutputKind.Info); lblResult.Text = "Stopped"; }
        else
        {
            AppendLine(result.ExitCode == 0 ? $"✔ Finished in {secs}" : $"✖ Exit code {result.ExitCode} after {secs}",
                result.ExitCode == 0 ? OutputKind.Info : OutputKind.Stderr);
            lblResult.Text = result.ExitCode == 0 ? "Succeeded" : $"Exit code {result.ExitCode}";
        }
        lblElapsed.Text = secs;
        FlushQueue();
        SetRunning(false);
        MainWindow.Instance?.SetStatus($"{tool.Name}: {lblResult.Text}");
    }

    private void SetRunning(bool running)
    {
        progress.Visibility = running ? Visibility.Visible : Visibility.Hidden;
        progress.IsIndeterminate = running;
        pnlInput.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        if (running) txtInput.Focus();
        UpdateButtons();
    }

    // ---------- stdin ----------
    private void BtnSend_Click(object sender, RoutedEventArgs e) => SendInput();
    private void TxtInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { SendInput(); e.Handled = true; } }
    private void SendInput()
    {
        if (!IsRunning) return;
        string text = txtInput.Text;
        AppendLine("> " + text, OutputKind.Info);
        _runner.SendInput(text);
        txtInput.Clear();
    }

    // ---------- terminal ----------
    private void AppendLine(string text, OutputKind kind)
    {
        var span = new Span();
        if (kind == OutputKind.Info) span.SetResourceReference(TextElement.ForegroundProperty, "TerminalInfoBrush");
        else if (kind == OutputKind.Stderr) span.SetResourceReference(TextElement.ForegroundProperty, "TerminalErrBrush");
        string prefix = ConfigService.Settings.ShowTimestamps ? DateTime.Now.ToString("HH:mm:ss  ") : "";
        span.Inlines.Add(new Run(prefix + text));
        span.Inlines.Add(new LineBreak());
        _para.Inlines.Add(span);
        _lineCount++;
        int max = ConfigService.Settings.MaxOutputLines;
        while (_lineCount > max && _para.Inlines.FirstInline != null) { _para.Inlines.Remove(_para.Inlines.FirstInline); _lineCount--; }
    }

    private void FlushQueue()
    {
        bool any = false;
        for (int i = 0; i < 800 && _queue.TryDequeue(out var item); i++) { AppendLine(item.Text, item.Kind); any = true; }
        if (any && chkScroll.IsChecked == true) txtTerminal.ScrollToEnd();
    }

    private void ClearTerminal() { _para.Inlines.Clear(); _lineCount = 0; }
    private string AllText() => new TextRange(txtTerminal.Document.ContentStart, txtTerminal.Document.ContentEnd).Text;

    private void BtnClear_Click(object sender, RoutedEventArgs e) { ClearTerminal(); lblResult.Text = "Ready"; lblElapsed.Text = ""; }
    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(AllText()); MainWindow.Instance?.SetStatus("Output copied to clipboard."); }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Log file (*.log)|*.log|Text file (*.txt)|*.txt",
            FileName = $"{(Selected?.Name ?? "output")}-{DateTime.Now:yyyyMMdd-HHmmss}.log".Replace(':', '-')
        };
        if (dlg.ShowDialog() != true) return;
        try { System.IO.File.WriteAllText(dlg.FileName, AllText()); MainWindow.Instance?.SetStatus("Saved " + dlg.FileName); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Save failed"); }
    }

    private void Wrap_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        txtTerminal.Document.PageWidth = chkWrap.IsChecked == true ? double.NaN : 4000;
        ConfigService.Settings.WrapOutput = chkWrap.IsChecked == true;
    }
    private void Scroll_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        ConfigService.Settings.AutoScroll = chkScroll.IsChecked == true;
    }

    private void ApplyTerminalSettings()
    {
        txtTerminal.Document.PageWidth = ConfigService.Settings.WrapOutput ? double.NaN : 4000;
        chkWrap.IsChecked = ConfigService.Settings.WrapOutput;
        chkScroll.IsChecked = ConfigService.Settings.AutoScroll;
    }

    // ---------- tool management ----------
    private void BtnNew_Click(object sender, RoutedEventArgs e) => NewTool();
    private void NewTool()
    {
        var seed = new ToolItem { Shell = ConfigService.Settings.DefaultShell };
        if (_category != AllCategories && _category != FavoritesOnly) seed.Category = _category;
        var win = new ToolEditorWindow(seed, isNew: true) { Owner = Window.GetWindow(this) };
        if (win.ShowDialog() != true) return;
        ConfigService.Current.Tools.Add(win.Result);
        ConfigService.Save();
        SelectTool(win.Result, false);
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e) => EditSelected();
    private void EditSelected()
    {
        if (Selected is not ToolItem tool) return;
        var win = new ToolEditorWindow(tool, isNew: false) { Owner = Window.GetWindow(this) };
        if (win.ShowDialog() != true) return;
        tool.CopyFrom(win.Result);
        ConfigService.Save();
        RebuildCategories();
        lstTools.SelectedItem = tool;
    }

    private void BtnDuplicate_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not ToolItem tool) return;
        var copy = tool.Clone();
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name += " (copy)";
        copy.LastRun = null; copy.RunCount = 0;
        ConfigService.Current.Tools.Add(copy);
        ConfigService.Save();
        SelectTool(copy, false);
    }

    private void BtnFav_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not ToolItem tool) return;
        tool.Favorite = !tool.Favorite;
        ConfigService.Save();
        _view.Refresh();
        lstTools.SelectedItem = tool;
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e) => DeleteSelected();
    private void DeleteSelected()
    {
        if (Selected is not ToolItem tool) return;
        if (ConfigService.Settings.ConfirmDelete &&
            MessageBox.Show(Window.GetWindow(this), $"Delete “{tool.Name}”?", "Delete tool", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        ConfigService.Current.Tools.Remove(tool);
        ConfigService.Save();
    }

    // ---------- keyboard / drag&drop ----------
    private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers == ModifierKeys.Control;
        bool typing = Keyboard.FocusedElement is TextBox;
        if (ctrl && e.Key == Key.F) { txtSearch.Focus(); txtSearch.SelectAll(); e.Handled = true; }
        else if (ctrl && e.Key == Key.N) { NewTool(); e.Handled = true; }
        else if (e.Key == Key.F5 || (ctrl && e.Key == Key.Enter)) { _ = RunSelectedAsync(false); e.Handled = true; }
        else if (e.Key == Key.Escape) { if (IsRunning) StopRunning(); else txtSearch.Clear(); e.Handled = true; }
        else if (!typing && e.Key == Key.F2) { EditSelected(); e.Handled = true; }
        else if (!typing && e.Key == Key.Delete) { DeleteSelected(); e.Handled = true; }
        else if (!typing && e.Key == Key.Enter && lstTools.IsKeyboardFocusWithin) { _ = RunSelectedAsync(false); e.Handled = true; }
    }

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Root_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        int added = 0, skipped = 0;
        ToolItem? last = null;
        foreach (var f in files)
        {
            var t = ToolFactory.FromPath(f);
            if (t == null) { skipped++; continue; }
            if (ConfigService.Current.Tools.Any(x => x.Command == t.Command && x.Shell == t.Shell)) { skipped++; continue; }
            ConfigService.Current.Tools.Add(t); last = t; added++;
        }
        if (added > 0) ConfigService.Save();
        MainWindow.Instance?.SetStatus($"Added {added} tool(s) via drag & drop" + (skipped > 0 ? $", skipped {skipped} (duplicate or unsupported)." : "."));
        if (last != null) SelectTool(last, false);
    }
}
