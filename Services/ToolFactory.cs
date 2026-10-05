using System.Text.RegularExpressions;
using ToolkitApp.Models;

namespace ToolkitApp.Services;

/// <summary>Builds ToolItems from dropped files / cloned repositories.</summary>
public static class ToolFactory
{
    public static ToolItem? FromPath(string path, string defaultCategory = "Dropped")
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        path = path.Trim().Trim('"');
        bool isDir = Directory.Exists(path);
        if (!isDir && !File.Exists(path)) return null;

        string name = isDir ? new DirectoryInfo(path).Name : Path.GetFileNameWithoutExtension(path);
        string dir = isDir ? path : (Path.GetDirectoryName(path) ?? "");
        string ext = isDir ? "" : Path.GetExtension(path).ToLowerInvariant();
        string q = $"\"{path}\"";

        var t = new ToolItem { Name = name, WorkingDirectory = dir, Category = defaultCategory };
        if (isDir) { t.Shell = "cmd"; t.Command = $"start \"\" {q}"; t.Category = "Folders"; return t; }

        switch (ext)
        {
            case ".ps1": t.Shell = "powershell"; t.Command = $"& {q}"; break;
            case ".bat": case ".cmd": case ".exe": case ".com": t.Shell = "cmd"; t.Command = q; break;
            case ".py": t.Shell = "cmd"; t.Command = $"python {q}"; break;
            case ".js": t.Shell = "cmd"; t.Command = $"node {q}"; break;
            case ".jar": t.Shell = "cmd"; t.Command = $"java -jar {q}"; break;
            case ".sh": t.Shell = "wsl"; t.Command = $"bash \"{ToWslPath(path)}\""; break;
            default: t.Shell = "cmd"; t.Command = $"start \"\" {q}"; break;
        }
        return t;
    }

    public static string ToWslPath(string winPath)
    {
        var m = Regex.Match(winPath, @"^([A-Za-z]):[\\/](.*)$");
        return m.Success ? $"/mnt/{char.ToLowerInvariant(m.Groups[1].Value[0])}/{m.Groups[2].Value.Replace('\\', '/')}" : winPath.Replace('\\', '/');
    }

    /// <summary>Tools for a freshly cloned repo: update, open folder, plus runnable scripts in its root.</summary>
    public static List<ToolItem> FromRepository(string repoDir, string fullName)
    {
        var list = new List<ToolItem>
        {
            new() { Name = $"Update {fullName}", Command = $"git -C \"{repoDir}\" pull", Shell = "cmd", Category = "GitHub", WorkingDirectory = repoDir, Description = "git pull" },
            new() { Name = $"Open {fullName} folder", Command = $"start \"\" \"{repoDir}\"", Shell = "cmd", Category = "GitHub", WorkingDirectory = repoDir }
        };
        try
        {
            var exts = new[] { ".bat", ".cmd", ".ps1", ".exe" };
            foreach (var f in Directory.EnumerateFiles(repoDir).Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => f).Take(6))
            {
                var t = FromPath(f, "GitHub");
                if (t != null) { t.Name = $"{fullName}: {Path.GetFileName(f)}"; list.Add(t); }
            }
        }
        catch { /* unreadable directory: just return the basics */ }
        return list;
    }

    public static List<ToolItem> StarterPack() => new()
    {
        T("Ping Google", "ping google.com", "cmd", "Network"),
        T("IP configuration", "ipconfig /all", "cmd", "Network"),
        T("Flush DNS cache", "ipconfig /flushdns", "cmd", "Network", admin: true),
        T("Trace route", "tracert {{host:google.com}}", "cmd", "Network", "Prompts for a host"),
        T("DNS lookup", "nslookup {{host:example.com}}", "cmd", "Network"),
        T("Open connections", "netstat -ano", "cmd", "Network"),
        T("Saved Wi-Fi profiles", "netsh wlan show profiles", "cmd", "Network"),
        T("System info", "systeminfo", "cmd", "System"),
        T("Running processes", "tasklist", "cmd", "System"),
        T("Disk usage", "Get-PSDrive -PSProvider FileSystem | Format-Table -AutoSize", "powershell", "System"),
        T("Services", "Get-Service | Sort-Object Status | Format-Table -AutoSize", "powershell", "System"),
        T("Startup programs", "Get-CimInstance Win32_StartupCommand | Select-Object Name,Command,Location | Format-List", "powershell", "System"),
        T("System file check", "sfc /scannow", "cmd", "Repair", admin: true),
        T("Repair Windows image", "DISM /Online /Cleanup-Image /RestoreHealth", "cmd", "Repair", admin: true),
        T("Upgrade all apps (winget)", "winget upgrade --all", "cmd", "Dev", admin: true),
        T("Tool versions", "git --version & node -v & python --version & dotnet --version", "cmd", "Dev"),
    };

    private static ToolItem T(string n, string c, string s, string cat, string d = "", bool admin = false) =>
        new() { Name = n, Command = c, Shell = s, Category = cat, Description = d, RunAsAdmin = admin };
}
