using System.Text.RegularExpressions;
using ToolkitApp.Models;

namespace ToolkitApp.Services;

public static class ReadmeLocator
{
    private static readonly string[] Names = { "README.md", "readme.md", "README.txt", "readme.txt", "README" };

    public static string? Find(ToolItem tool, string toolsDirectory)
    {
        var dirs = new List<string>();
        if (!string.IsNullOrWhiteSpace(tool.WorkingDirectory)) dirs.Add(tool.WorkingDirectory);

        var clone = Regex.Match(tool.Command, @"git\s+clone\s+(?:--\S+\s+)*""?(\S+?)""?(?:\s|$)", RegexOptions.IgnoreCase);
        if (clone.Success)
        {
            var repo = clone.Groups[1].Value.TrimEnd('/', '\\').Split('/', '\\').Last();
            if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) repo = repo[..^4];
            if (repo.Length > 0) { dirs.Add(Path.Combine(toolsDirectory, repo)); dirs.Add(Path.Combine(Environment.CurrentDirectory, repo)); }
        }
        foreach (Match m in Regex.Matches(tool.Command, "\"([^\"]+)\""))
        {
            var p = m.Groups[1].Value;
            try
            {
                if (Directory.Exists(p)) dirs.Add(p);
                else if (File.Exists(p) && Path.GetDirectoryName(p) is string d) dirs.Add(d);
            }
            catch { }
        }

        foreach (var d in dirs.Distinct())
        {
            try
            {
                if (!Directory.Exists(d)) continue;
                foreach (var n in Names) { var f = Path.Combine(d, n); if (File.Exists(f)) return f; }
            }
            catch { }
        }
        return null;
    }
}
