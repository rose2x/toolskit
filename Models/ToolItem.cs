using System.Text.Json.Serialization;

namespace ToolkitApp.Models;

/// <summary>A runnable command. Backwards compatible with the v0.1 config (Name/Command/Shell).</summary>
public class ToolItem : ObservableObject
{
    public static readonly string[] Shells = { "cmd", "powershell", "pwsh", "wsl", "direct" };

    private string _name = "";
    private string _command = "";
    private string _shell = "cmd";
    private string _category = "General";
    private string _description = "";
    private string _workingDirectory = "";
    private bool _runAsAdmin;
    private bool _keepWindowOpen = true;
    private bool _favorite;
    private int _timeoutSeconds;
    private DateTime? _lastRun;
    private int _runCount;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get => _name; set => Set(ref _name, (value ?? "").Trim()); }
    public string Command { get => _command; set => Set(ref _command, value ?? ""); }

    /// <summary>cmd | powershell | pwsh | wsl | direct (run an executable with arguments).</summary>
    public string Shell
    {
        get => _shell;
        set
        {
            var s = (value ?? "cmd").Trim().ToLowerInvariant();
            if (Array.IndexOf(Shells, s) < 0) s = "cmd";
            if (Set(ref _shell, s)) Raise(nameof(Badge));
        }
    }

    public string Category
    {
        get => _category;
        set => Set(ref _category, string.IsNullOrWhiteSpace(value) ? "General" : value.Trim());
    }

    public string Description { get => _description; set { if (Set(ref _description, (value ?? "").Trim())) Raise(nameof(Tooltip)); } }
    public string WorkingDirectory { get => _workingDirectory; set => Set(ref _workingDirectory, (value ?? "").Trim().Trim('"')); }

    public bool RunAsAdmin { get => _runAsAdmin; set { if (Set(ref _runAsAdmin, value)) Raise(nameof(Badge)); } }

    /// <summary>For elevated runs: keep the elevated console open (true) or capture output into the app (false).</summary>
    public bool KeepWindowOpen { get => _keepWindowOpen; set => Set(ref _keepWindowOpen, value); }

    public bool Favorite { get => _favorite; set { if (Set(ref _favorite, value)) Raise(nameof(FavoriteGlyph)); } }

    /// <summary>0 = no timeout.</summary>
    public int TimeoutSeconds { get => _timeoutSeconds; set => Set(ref _timeoutSeconds, Math.Max(0, value)); }

    private Dictionary<string, string> _envVars = new();
    public Dictionary<string, string> EnvVars { get => _envVars; set => _envVars = value ?? new(); }

    public DateTime? LastRun { get => _lastRun; set { if (Set(ref _lastRun, value)) Raise(nameof(LastRunText)); } }
    public int RunCount { get => _runCount; set => Set(ref _runCount, value); }

    [JsonIgnore] public string FavoriteGlyph => Favorite ? "★" : "☆";
    [JsonIgnore] public string Badge => RunAsAdmin ? $"{Shell} · admin" : Shell;
    [JsonIgnore] public string LastRunText => LastRun is DateTime d ? "Last run " + d.ToString("g") : "Never run";
    [JsonIgnore] public string Tooltip => string.IsNullOrWhiteSpace(Description) ? Command : Description + "\n\n" + Command;

    public ToolItem Clone()
    {
        var c = new ToolItem { Id = Id };
        c.CopyFrom(this);
        c.LastRun = LastRun;
        c.RunCount = RunCount;
        return c;
    }

    /// <summary>Copies editable fields (not Id / run history).</summary>
    public void CopyFrom(ToolItem o)
    {
        Name = o.Name; Command = o.Command; Shell = o.Shell; Category = o.Category;
        Description = o.Description; WorkingDirectory = o.WorkingDirectory;
        RunAsAdmin = o.RunAsAdmin; KeepWindowOpen = o.KeepWindowOpen; Favorite = o.Favorite;
        TimeoutSeconds = o.TimeoutSeconds;
        EnvVars = new Dictionary<string, string>(o.EnvVars);
    }

    public static string FormatEnv(IDictionary<string, string> env) =>
        string.Join(Environment.NewLine, env.Select(kv => kv.Key + "=" + kv.Value));

    /// <summary>Parses KEY=VALUE lines. Returns null and an error message for the first bad line.</summary>
    public static Dictionary<string, string>? ParseEnv(string text, out string? error)
    {
        error = null;
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = (text ?? "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim('\r', ' ', '\t');
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) { error = $"Line {i + 1}: expected KEY=VALUE"; return null; }
            var key = line[..eq].Trim();
            if (key.Any(ch => char.IsWhiteSpace(ch) || ch == '"')) { error = $"Line {i + 1}: invalid variable name '{key}'"; return null; }
            result[key] = line[(eq + 1)..];
        }
        return result;
    }

    public override string ToString() => $"{Name} ({Shell})";
}
