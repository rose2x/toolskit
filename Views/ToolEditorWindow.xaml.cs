using System.Windows;
using System.Windows.Controls;
using ToolkitApp.Models;
using ToolkitApp.Services;

namespace ToolkitApp.Views;

/// <summary>Edits a copy of a tool; the caller applies <see cref="Result"/> only when the dialog returns true.</summary>
public partial class ToolEditorWindow : Window
{
    public ToolItem Result { get; }

    private static readonly Dictionary<string, string> Hints = new()
    {
        ["cmd"] = "Windows Command Prompt.",
        ["powershell"] = "Windows PowerShell 5.1.",
        ["pwsh"] = "PowerShell 7+ (must be installed).",
        ["wsl"] = "Runs inside your default WSL distro with bash.",
        ["direct"] = "Starts an executable directly: program.exe arg1 arg2."
    };

    public ToolEditorWindow(ToolItem tool, bool isNew)
    {
        InitializeComponent();
        ThemeService.Attach(this);
        Result = tool.Clone();
        Title = isNew ? "New tool" : "Edit tool";
        lblHeader.Text = isNew ? "New tool" : "Edit tool";

        cmbShell.ItemsSource = ToolItem.Shells;
        cmbShell.SelectedItem = Result.Shell;
        cmbCategory.ItemsSource = ConfigService.Current.Tools.Select(t => t.Category).Append("General").Distinct().OrderBy(c => c).ToList();
        cmbCategory.Text = Result.Category;

        txtName.Text = Result.Name;
        txtCommand.Text = Result.Command;
        txtDescription.Text = Result.Description;
        txtWorkDir.Text = Result.WorkingDirectory;
        txtEnv.Text = ToolItem.FormatEnv(Result.EnvVars);
        txtTimeout.Text = Result.TimeoutSeconds.ToString();
        chkFavorite.IsChecked = Result.Favorite;
        chkAdmin.IsChecked = Result.RunAsAdmin;
        chkKeepOpen.IsChecked = Result.KeepWindowOpen;

        LoadReadme(isNew);
        Loaded += (_, _) => { txtName.Focus(); txtName.SelectAll(); };
    }

    private void LoadReadme(bool isNew)
    {
        try
        {
            var path = isNew ? null : ReadmeLocator.Find(Result, ConfigService.ResolveToolsDirectory());
            txtReadme.Text = path != null ? System.IO.File.ReadAllText(path)
                : "No README found next to this tool. If it came from GitHub, use “Clone & add” on the GitHub page so a local copy exists.";
        }
        catch (Exception ex) { txtReadme.Text = "Could not read README: " + ex.Message; }
    }

    private void CmbShell_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (cmbShell.SelectedItem is string s) lblShellHint.Text = Hints.GetValueOrDefault(s, "");
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Choose working directory" };
        if (dlg.ShowDialog(this) == true) txtWorkDir.Text = dlg.FolderName;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text) || string.IsNullOrWhiteSpace(txtCommand.Text))
        {
            MessageBox.Show(this, "Name and command are required.", "Tool Kit", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!int.TryParse(txtTimeout.Text.Trim(), out int timeout) || timeout < 0)
        {
            MessageBox.Show(this, "Timeout must be a whole number of seconds (0 or more).", "Tool Kit", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var env = ToolItem.ParseEnv(txtEnv.Text, out var envError);
        if (env == null)
        {
            MessageBox.Show(this, envError, "Environment variables", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var wd = txtWorkDir.Text.Trim().Trim('"');
        if (wd.Length > 0 && !System.IO.Directory.Exists(wd) &&
            MessageBox.Show(this, "That working directory does not exist. Save anyway?", "Tool Kit", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        Result.Name = txtName.Text;
        Result.Command = txtCommand.Text;
        Result.Shell = cmbShell.SelectedItem as string ?? "cmd";
        Result.Category = cmbCategory.Text;
        Result.Description = txtDescription.Text;
        Result.WorkingDirectory = wd;
        Result.EnvVars = env;
        Result.TimeoutSeconds = timeout;
        Result.Favorite = chkFavorite.IsChecked == true;
        Result.RunAsAdmin = chkAdmin.IsChecked == true;
        Result.KeepWindowOpen = chkKeepOpen.IsChecked == true;
        DialogResult = true;
    }
}
