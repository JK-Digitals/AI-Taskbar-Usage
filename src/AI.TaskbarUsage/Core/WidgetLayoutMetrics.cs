#nullable enable

namespace AiTaskbarUsage;

public static class WidgetLayoutMetrics
{
    public const int HeightDip = 30;

    public static int WidthDip(LayoutMode layout, bool showClaude, bool showCodex)
    {
        var visibleServices = (showClaude ? 1 : 0) + (showCodex ? 1 : 0);
        if (visibleServices <= 1)
        {
            return layout == LayoutMode.Compact ? 104 : 154;
        }

        return layout == LayoutMode.Compact ? 166 : 292;
    }
}
