using System.Reflection;

namespace ToolkitApp.Services;

public static class AppInfo
{
    public static string Version
    {
        get
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var v = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrWhiteSpace(v)) v = asm.GetName().Version?.ToString(3) ?? "0.0.0";
            int plus = v.IndexOf('+');
            return plus > 0 ? v[..plus] : v;
        }
    }
    public const string RepoUrl = "https://github.com/rose2x/toolskit";
}
