using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ToolkitApp.Services;

namespace ToolkitApp.Views;

/// <summary>Asks for {{placeholder}} values before a tool runs. Built in code so it adapts to any parameter list.</summary>
public sealed class ParameterDialog : Window
{
    private readonly Dictionary<string, TextBox> _boxes = new();

    private ParameterDialog(string title, IReadOnlyList<Placeholder> parameters)
    {
        Title = title; SizeToContent = SizeToContent.Height; Width = 440; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "BgBaseBrush");
        SetResourceReference(ForegroundProperty, "TextBrush");
        SetResourceReference(FontSizeProperty, "UiFontSize");
        ThemeService.Attach(this);

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "Enter values for this run", FontSize = 18, Margin = new Thickness(0, 0, 0, 14) });
        foreach (var p in parameters)
        {
            panel.Children.Add(new TextBlock { Text = p.Name, Margin = new Thickness(0, 0, 0, 3) });
            var tb = new TextBox { Text = p.Default, Margin = new Thickness(0, 0, 0, 12) };
            tb.KeyDown += (_, e) => { if (e.Key == Key.Enter) { DialogResult = true; } };
            _boxes[p.Name] = tb;
            panel.Children.Add(tb);
        }
        var ok = new Button { Content = "Run", Width = 90, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        ok.Click += (_, _) => DialogResult = true;
        var cancel = new Button { Content = "Cancel", Width = 90, IsCancel = true, Style = (Style)Application.Current.FindResource("SecondaryButton") };
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } });
        Content = panel;
        Loaded += (_, _) => { var first = _boxes.Values.FirstOrDefault(); first?.Focus(); first?.SelectAll(); };
    }

    public static Dictionary<string, string>? Ask(Window? owner, string toolName, IReadOnlyList<Placeholder> parameters)
    {
        var dlg = new ParameterDialog(toolName, parameters) { Owner = owner };
        return dlg.ShowDialog() == true ? dlg._boxes.ToDictionary(kv => kv.Key, kv => kv.Value.Text) : null;
    }
}
