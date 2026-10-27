#nullable enable
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using WpfButton = System.Windows.Controls.Button;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace AiTaskbarUsage;

public sealed class DetailsWindow : Window
{
    public DetailsWindow(
        UsageSnapshot claude,
        UsageSnapshot codex,
        bool showClaude,
        bool showCodex,
        Action refresh,
        Action openClaude,
        Action openCodex)
    {
        Title = "AI Taskbar Usage";
        Width = 430;
        Height = showClaude && showCodex ? 330 : 250;
        MinWidth = 360;
        MinHeight = 240;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(MediaColor.FromRgb(245, 246, 248));

        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock
        {
            Text = "Usage details",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 14)
        });
        if (showClaude)
        {
            panel.Children.Add(CreateCard("Claude Code", claude));
        }
        if (showCodex)
        {
            panel.Children.Add(CreateCard("OpenAI Codex", codex));
        }

        var buttons = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(Button("Refresh", refresh));
        if (showClaude)
        {
            buttons.Children.Add(Button("Open Claude", openClaude));
        }
        if (showCodex)
        {
            buttons.Children.Add(Button("Open Codex", openCodex));
        }
        panel.Children.Add(buttons);
        Content = panel;
    }

    private static Border CreateCard(string name, UsageSnapshot snapshot)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, FontSize = 14 });
        panel.Children.Add(new TextBlock { Text = Describe("5-hour", snapshot.FiveHour), Margin = new Thickness(0, 4, 0, 0) });
        panel.Children.Add(new TextBlock { Text = Describe("7-day", snapshot.Weekly) });
        panel.Children.Add(new TextBlock
        {
            Text = $"Status: {snapshot.State}{(snapshot.IsStale ? " (stale)" : string.Empty)}{(string.IsNullOrWhiteSpace(snapshot.Message) ? string.Empty : " — " + snapshot.Message)}",
            Foreground = WpfBrushes.DimGray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });
        return new Border
        {
            Background = WpfBrushes.White,
            BorderBrush = new SolidColorBrush(MediaColor.FromRgb(222, 225, 230)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8),
            Child = panel
        };
    }

    private static string Describe(string label, QuotaWindow window)
    {
        var value = window.UsedPercent is null ? "—" : $"{window.UsedPercent:0.#}%";
        var reset = window.ResetAt is null ? "reset unknown" : $"reset {window.ResetAt.Value.ToLocalTime():g}";
        return $"{label}: {value} · {reset}";
    }

    private static WpfButton Button(string text, Action action)
    {
        var button = new WpfButton { Content = text, Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 8, 0) };
        button.Click += (_, _) => action();
        return button;
    }
}
