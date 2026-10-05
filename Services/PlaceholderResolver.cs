using System.Text.RegularExpressions;

namespace ToolkitApp.Services;

public sealed record Placeholder(string Name, string Default);

/// <summary>Supports {{name}} and {{name:default}} parameters inside a command.</summary>
public static class PlaceholderResolver
{
    private static readonly Regex Rx = new(@"\{\{\s*([A-Za-z_][\w\- ]*?)\s*(?::([^{}]*))?\}\}", RegexOptions.Compiled);

    public static List<Placeholder> Find(string command)
    {
        var list = new List<Placeholder>();
        foreach (Match m in Rx.Matches(command ?? ""))
        {
            var name = m.Groups[1].Value;
            if (list.Any(p => p.Name == name)) continue;
            list.Add(new Placeholder(name, m.Groups[2].Success ? m.Groups[2].Value : ""));
        }
        return list;
    }

    public static string Apply(string command, IDictionary<string, string> values) =>
        Rx.Replace(command ?? "", m =>
        {
            var name = m.Groups[1].Value;
            if (values.TryGetValue(name, out var v) && v.Length > 0) return v;
            return m.Groups[2].Success ? m.Groups[2].Value : "";
        });
}
