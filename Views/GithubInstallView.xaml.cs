using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ToolkitApp.Models;
using ToolkitApp.Services;

namespace ToolkitApp.Views;

public partial class GithubInstallView : UserControl
{
    private readonly List<GithubRepo> _results = new();
    private CancellationTokenSource? _readmeCts;
    private int _page = 1, _total;
    private string _lastQuery = "", _lastLang = "", _lastSort = "";
    private bool _busy;

    public GithubInstallView()
    {
        InitializeComponent();
        txtQuery.TextChanged += (_, _) => lblQueryHint.Visibility = txtQuery.Text.Length == 0 ? Visibility.Visible : Visibility.Hidden;
        txtLanguage.TextChanged += (_, _) => lblLangHint.Visibility = txtLanguage.Text.Length == 0 ? Visibility.Visible : Visibility.Hidden;
        Loaded += (_, _) => txtQuery.Focus();
    }

    private string? Token => GithubService.GetToken(ConfigService.Settings.GithubToken);
    private GithubRepo? Selected => lstResults.SelectedItem as GithubRepo;

    private void SetBusy(bool busy)
    {
        _busy = busy;
        progress.Visibility = busy ? Visibility.Visible : Visibility.Hidden;
        progress.IsIndeterminate = busy;
        btnSearch.IsEnabled = !busy;
        btnMore.IsEnabled = !busy;
        UpdateButtons();
    }

    private void UpdateButtons() =>
        btnClone.IsEnabled = btnAddCmd.IsEnabled = btnOpen.IsEnabled = Selected != null && !_busy;

    private void TxtQuery_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) _ = SearchAsync(reset: true); }
    private void BtnSearch_Click(object sender, RoutedEventArgs e) => _ = SearchAsync(reset: true);
    private void BtnMore_Click(object sender, RoutedEventArgs e) => _ = SearchAsync(reset: false);

    private async Task SearchAsync(bool reset)
    {
        if (_busy) return;
        if (reset)
        {
            _lastQuery = txtQuery.Text.Trim();
            _lastLang = txtLanguage.Text.Trim();
            _lastSort = (cmbSort.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            _page = 1;
        }
        else _page++;
        if (_lastQuery.Length == 0) { lblStatus.Text = "Type something to search for."; return; }

        SetBusy(true);
        lblStatus.Text = "Searching GitHub…";
        try
        {
            int size = ConfigService.Settings.GithubPageSize;
            var res = await GithubService.SearchAsync(_lastQuery, _lastLang, _lastSort, _page, size, Token);
            if (reset) _results.Clear();
            _results.AddRange(res.Items);
            _total = res.TotalCount;
            lstResults.ItemsSource = null;
            lstResults.ItemsSource = _results;
            btnMore.Visibility = _results.Count < Math.Min(_total, 1000) && res.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            lblStatus.Text = _results.Count == 0 ? "No repositories found for that query." : $"Showing {_results.Count} of {_total:N0} repositories.";
        }
        catch (GithubException ex) { lblStatus.Text = ex.Message; if (!reset) _page--; }
        catch (TaskCanceledException) { lblStatus.Text = "GitHub did not answer in time. Check your connection."; if (!reset) _page--; }
        catch (HttpRequestException ex) { lblStatus.Text = "Network error: " + ex.Message; if (!reset) _page--; }
        catch (Exception ex) { lblStatus.Text = "Unexpected error: " + ex.Message; ConfigService.Log("GitHub search", ex); }
        finally { SetBusy(false); }
    }

    private async void LstResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateButtons();
        _readmeCts?.Cancel();
        if (Selected is not GithubRepo repo) { lblRepoTitle.Text = "Select a repository"; lblRepoMeta.Text = ""; txtReadme.Text = ""; return; }
        lblRepoTitle.Text = repo.FullName;
        lblRepoMeta.Text = repo.Meta;
        txtReadme.Text = "Loading README…";
        _readmeCts = new CancellationTokenSource();
        var ct = _readmeCts.Token;
        try
        {
            await Task.Delay(250, ct);   // debounce quick arrow-key browsing
            var text = await GithubService.GetReadmeAsync(repo.FullName, Token, ct);
            if (!ct.IsCancellationRequested) txtReadme.Text = text;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!ct.IsCancellationRequested) txtReadme.Text = "Could not load README: " + ex.Message; }
    }

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is GithubRepo r && r.HtmlUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            Process.Start(new ProcessStartInfo(r.HtmlUrl) { UseShellExecute = true });
    }

    private void BtnAddCommand_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not GithubRepo r) return;
        if (!GithubService.IsSafeCloneUrl(r.CloneUrl) || !GithubService.IsSafeRepoName(r.FullName)) { lblStatus.Text = "Refusing to add: unexpected repository address."; return; }
        string dest = System.IO.Path.Combine(ConfigService.ResolveToolsDirectory(), r.RepoName);
        var tool = new ToolItem
        {
            Name = "GitHub: " + r.FullName, Category = "GitHub", Shell = "cmd", Description = r.Description ?? "",
            Command = $"git clone --depth 1 \"{r.CloneUrl}\" \"{dest}\""
        };
        if (ConfigService.Current.Tools.Any(t => t.Command == tool.Command)) { lblStatus.Text = "That clone command is already in your tools."; return; }
        ConfigService.Current.Tools.Add(tool);
        ConfigService.Save();
        lblStatus.Text = $"Added “{tool.Name}”. Run it from Run Tools to clone.";
    }

    private async void BtnClone_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not GithubRepo r || _busy) return;
        if (!GithubService.IsSafeCloneUrl(r.CloneUrl) || !GithubService.IsSafeRepoName(r.FullName)) { lblStatus.Text = "Refusing to clone: unexpected repository address."; return; }

        string dest = System.IO.Path.Combine(ConfigService.ResolveToolsDirectory(), r.RepoName);
        txtLog.Visibility = Visibility.Visible;
        txtLog.Clear();
        SetBusy(true);
        try
        {
            if (System.IO.Directory.Exists(dest) && System.IO.Directory.EnumerateFileSystemEntries(dest).Any())
            {
                Log($"{dest} already exists – skipping clone.");
            }
            else
            {
                lblStatus.Text = $"Cloning {r.FullName}…";
                var runner = new CommandRunner();
                runner.Output += (text, _) => Dispatcher.InvokeAsync(() => Log(text));
                var tool = new ToolItem { Shell = "cmd", Command = $"git clone --depth 1 \"{r.CloneUrl}\" \"{dest}\"" };
                var res = await runner.RunAsync(tool, tool.Command, admin: false);
                if (res.Error != null || res.ExitCode != 0)
                {
                    lblStatus.Text = res.Error ?? $"git exited with code {res.ExitCode}. Is Git installed? (winget install Git.Git)";
                    return;
                }
            }

            int added = 0;
            foreach (var t in ToolFactory.FromRepository(dest, r.FullName))
            {
                if (ConfigService.Current.Tools.Any(x => x.Command == t.Command && x.Shell == t.Shell)) continue;
                ConfigService.Current.Tools.Add(t); added++;
            }
            ConfigService.Save();
            lblStatus.Text = $"Cloned to {dest}. Added {added} tool(s) – find them under the “GitHub” category.";
        }
        catch (Exception ex) { lblStatus.Text = "Clone failed: " + ex.Message; ConfigService.Log("Clone", ex); }
        finally { SetBusy(false); }
    }

    private void Log(string line)
    {
        txtLog.AppendText(line + Environment.NewLine);
        txtLog.ScrollToEnd();
    }
}
