using System.Collections.ObjectModel;
using System.Text.Json;
using ToolkitApp.Models;

namespace ToolkitApp.Services;

/// <summary>Loads/saves %AppData%\ToolKit\config.json. Single source of truth for settings and tools.</summary>
public static class ConfigService
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string ConfigDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ToolKit");
    public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");
    public static string LogDirectory => Path.Combine(ConfigDirectory, "logs");

    public static AppConfig Current { get; private set; } = new();
    public static AppSettings Settings => Current.Settings;
    public static string? LoadWarning { get; private set; }
    public static string? LastError { get; private set; }

    /// <summary>Raised after settings were saved/applied from the Settings page.</summary>
    public static event Action? Changed;
    public static void NotifyChanged() => Changed?.Invoke();

    public static void Load()
    {
        lock (Gate)
        {
            LoadWarning = null;
            try { Directory.CreateDirectory(ConfigDirectory); } catch { }
            Current = new AppConfig();

            if (File.Exists(ConfigPath))
            {
                try
                {
                    Current = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), Json) ?? new AppConfig();
                }
                catch (Exception ex)
                {
                    string bad = Path.Combine(ConfigDirectory, $"config.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                    try { File.Move(ConfigPath, bad); } catch { }
                    LoadWarning = $"Config could not be read ({ex.Message}). A backup was kept at {bad}.";
                    Log("Config load failed", ex);
                    Current = new AppConfig();
                }
            }
            Normalize();

            if (!Current.Settings.StarterPackOffered && Current.Tools.Count == 0)
            {
                MigrateLegacy();
                if (Current.Tools.Count == 0) AddStarterPack();
                Current.Settings.StarterPackOffered = true;
                Save();
            }
        }
    }

    private static void Normalize()
    {
        var s = Current.Settings;
        if (s.MaxOutputLines < 200) s.MaxOutputLines = 200;
        if (s.UiFontSize < 10 || s.UiFontSize > 24) s.UiFontSize = 14;
        if (s.TerminalFontSize < 8 || s.TerminalFontSize > 32) s.TerminalFontSize = 13;
        if (s.GithubPageSize is < 10 or > 100) s.GithubPageSize = 30;
        if (Array.IndexOf(ToolItem.Shells, (s.DefaultShell ?? "").ToLowerInvariant()) < 0) s.DefaultShell = "cmd";
        var seen = new HashSet<string>();
        foreach (var t in Current.Tools)
        {
            if (string.IsNullOrWhiteSpace(t.Id) || !seen.Add(t.Id)) { t.Id = Guid.NewGuid().ToString("N"); seen.Add(t.Id); }
            t.Category = t.Category; // re-run normalisation for null/empty values from old files
            t.Shell = t.Shell;
        }
    }

    /// <summary>Imports tools from a v0.1 tools_config.json sitting next to the exe / in the working directory.</summary>
    private static void MigrateLegacy()
    {
        foreach (var dir in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory }.Distinct())
        {
            var p = Path.Combine(dir, "tools_config.json");
            if (!File.Exists(p)) continue;
            try
            {
                var old = JsonSerializer.Deserialize<ToolExport>(File.ReadAllText(p), Json);
                foreach (var t in old?.Tools ?? new())
                {
                    if (string.IsNullOrWhiteSpace(t.Name) || string.IsNullOrWhiteSpace(t.Command)) continue;
                    if (t.Category == "General" && t.Command.TrimStart().StartsWith("git clone", StringComparison.OrdinalIgnoreCase))
                        t.Category = "GitHub";
                    t.Id = Guid.NewGuid().ToString("N");
                    Current.Tools.Add(t);
                }
                if (Current.Tools.Count > 0) { Log($"Migrated {Current.Tools.Count} tools from {p}", null); return; }
            }
            catch (Exception ex) { Log("Legacy migration failed", ex); }
        }
    }

    public static int AddStarterPack()
    {
        int n = 0;
        foreach (var t in ToolFactory.StarterPack())
        {
            if (Current.Tools.Any(x => x.Name.Equals(t.Name, StringComparison.OrdinalIgnoreCase))) continue;
            Current.Tools.Add(t); n++;
        }
        return n;
    }

    /// <summary>Atomic save (temp file + replace, keeps a .bak of the previous version).</summary>
    public static bool Save()
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                string tmp = ConfigPath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(Current, Json));
                if (File.Exists(ConfigPath)) File.Replace(tmp, ConfigPath, ConfigPath + ".bak");
                else File.Move(tmp, ConfigPath);
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Log("Config save failed", ex);
                return false;
            }
        }
    }

    public static string ResolveToolsDirectory()
    {
        var dir = string.IsNullOrWhiteSpace(Settings.ToolsDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ToolKit", "Tools")
            : Settings.ToolsDirectory;
        try { Directory.CreateDirectory(dir); } catch { }
        return dir;
    }

    public static void Export(string path)
    {
        var export = new ToolExport { Tools = Current.Tools.ToList() };
        File.WriteAllText(path, JsonSerializer.Serialize(export, Json));
    }

    /// <summary>Imports tools. Duplicates (same name+command+shell) are skipped. Returns number added.</summary>
    public static int Import(string path, bool replace)
    {
        var data = JsonSerializer.Deserialize<ToolExport>(File.ReadAllText(path), Json)
                   ?? throw new InvalidDataException("File contains no tools.");
        if (replace) Current.Tools.Clear();
        int added = 0;
        foreach (var t in data.Tools)
        {
            if (string.IsNullOrWhiteSpace(t.Name) || string.IsNullOrWhiteSpace(t.Command)) continue;
            if (Current.Tools.Any(x => x.Name == t.Name && x.Command == t.Command && x.Shell == t.Shell)) continue;
            t.Id = Guid.NewGuid().ToString("N");
            Current.Tools.Add(t); added++;
        }
        Normalize();
        Save();
        return added;
    }

    public static void Log(string message, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            string file = Path.Combine(LogDirectory, "toolkit.log");
            if (File.Exists(file) && new FileInfo(file).Length > 1_000_000) File.Move(file, file + ".old", true);
            File.AppendAllText(file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{(ex != null ? "\n" + ex : "")}\n");
        }
        catch { }
    }
}
