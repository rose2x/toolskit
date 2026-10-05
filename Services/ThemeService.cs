using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ToolkitApp.Models;

namespace ToolkitApp.Services;

/// <summary>Swaps the brush resources at runtime. All XAML uses DynamicResource so changes apply instantly.</summary>
public static class ThemeService
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static event Action? ThemeApplied;

    public static bool TryParseColor(string? text, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        try
        {
            if (ColorConverter.ConvertFromString(text.Trim()) is Color c) { color = Color.FromRgb(c.R, c.G, c.B); return true; }
        }
        catch { }
        return false;
    }

    private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static Color Blend(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R * t + b.R * (1 - t)), (byte)(a.G * t + b.G * (1 - t)), (byte)(a.B * t + b.B * (1 - t)));

    private static void SetBrush(string key, Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        Application.Current.Resources[key] = b;
    }

    public static void Apply(AppSettings s)
    {
        bool light = s.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase);
        if (!TryParseColor(s.AccentColor, out var accent)) accent = C("#007ACC");
        if (!TryParseColor(s.TerminalBackground, out var tbg)) tbg = C("#0C0C0C");
        if (!TryParseColor(s.TerminalForeground, out var tfg)) tfg = C("#D4D4D4");

        Color bgBase = light ? C("#F3F3F3") : C("#1E1E1E");
        Color panel = light ? C("#FFFFFF") : C("#252526");
        Color input = light ? C("#FFFFFF") : C("#2D2D30");
        Color text = light ? C("#1E1E1E") : C("#D4D4D4");

        SetBrush("BgBaseBrush", bgBase);
        SetBrush("BgPanelBrush", panel);
        SetBrush("BgInputBrush", input);
        SetBrush("LineBrush", light ? C("#D0D0D0") : C("#3F3F46"));
        SetBrush("TextBrush", text);
        SetBrush("MutedBrush", light ? C("#6B6B6B") : C("#8A8A8A"));
        SetBrush("HoverBrush", Blend(text, bgBase, 0.08));
        SetBrush("SelectedBrush", Blend(accent, bgBase, 0.28));
        SetBrush("AccentBrush", accent);
        double lum = 0.299 * accent.R + 0.587 * accent.G + 0.114 * accent.B;
        SetBrush("AccentTextBrush", lum > 165 ? Colors.Black : Colors.White);
        SetBrush("DangerBrush", C("#C93C20"));
        SetBrush("SuccessBrush", light ? C("#1A7F37") : C("#4EC9B0"));
        SetBrush("WarnBrush", C("#E5A000"));
        SetBrush("ScrollThumbBrush", light ? C("#B5B5B5") : C("#4A4A4F"));
        SetBrush("TerminalBgBrush", tbg);
        SetBrush("TerminalFgBrush", tfg);
        SetBrush("TerminalInfoBrush", Blend(accent, tfg, 0.6));
        SetBrush("TerminalErrBrush", C("#F14C4C"));

        var res = Application.Current.Resources;
        res["UiFontSize"] = s.UiFontSize;
        res["TerminalFontSizeRes"] = s.TerminalFontSize;
        res["TerminalFontFamilyRes"] = new FontFamily(string.IsNullOrWhiteSpace(s.TerminalFontFamily) ? "Consolas" : s.TerminalFontFamily);
        ThemeApplied?.Invoke();
    }

    /// <summary>Dark/light native title bar + re-applies when the theme changes.</summary>
    public static void Attach(Window w)
    {
        void Do()
        {
            try
            {
                var h = new WindowInteropHelper(w).Handle;
                if (h == IntPtr.Zero) return;
                int dark = ConfigService.Settings.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                DwmSetWindowAttribute(h, 20, ref dark, sizeof(int));
                DwmSetWindowAttribute(h, 19, ref dark, sizeof(int));
            }
            catch { }
        }
        w.SourceInitialized += (_, _) => Do();
        ThemeApplied += Do;
        w.Closed += (_, _) => ThemeApplied -= Do;
    }
}
