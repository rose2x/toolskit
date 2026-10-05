using System.Collections.ObjectModel;

namespace ToolkitApp.Models;

public class AppSettings
{
    // Appearance
    public string Theme { get; set; } = "Dark";              // Dark | Light
    public string AccentColor { get; set; } = "#007ACC";
    public double UiFontSize { get; set; } = 14;
    public string TerminalFontFamily { get; set; } = "Consolas";
    public double TerminalFontSize { get; set; } = 13;
    public string TerminalBackground { get; set; } = "#0C0C0C";
    public string TerminalForeground { get; set; } = "#D4D4D4";

    // Behaviour
    public string DefaultShell { get; set; } = "cmd";
    public bool ClearOutputOnRun { get; set; } = true;
    public bool ConfirmAdmin { get; set; } = true;
    public bool ConfirmDelete { get; set; } = true;
    public bool WrapOutput { get; set; } = true;
    public bool AutoScroll { get; set; } = true;
    public bool ShowTimestamps { get; set; } = false;
    public int MaxOutputLines { get; set; } = 5000;
    public string StartView { get; set; } = "Home";           // Home | Tools | Installed | Github

    // GitHub
    public string GithubToken { get; set; } = "";             // optional; env var GITHUB_TOKEN also works
    public int GithubPageSize { get; set; } = 30;
    public string ToolsDirectory { get; set; } = "";          // empty = %USERPROFILE%\ToolKit\Tools

    // Window
    public double WindowWidth { get; set; } = 1100;
    public double WindowHeight { get; set; } = 720;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool WindowMaximized { get; set; }

    public bool StarterPackOffered { get; set; }
}

public class AppConfig
{
    private ObservableCollection<ToolItem> _tools = new();
    private AppSettings _settings = new();

    public int SchemaVersion { get; set; } = 2;
    public AppSettings Settings { get => _settings; set => _settings = value ?? new AppSettings(); }
    public ObservableCollection<ToolItem> Tools { get => _tools; set => _tools = value ?? new ObservableCollection<ToolItem>(); }
}

/// <summary>Portable export format (tools only; never includes the GitHub token).</summary>
public class ToolExport
{
    public int SchemaVersion { get; set; } = 2;
    public List<ToolItem> Tools { get; set; } = new();
}
