#nullable enable
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using WpfButton = System.Windows.Controls.Button;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using WpfControl = System.Windows.Controls.Control;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfTextBox = System.Windows.Controls.TextBox;
using WinFormsScreen = System.Windows.Forms.Screen;

namespace AiTaskbarUsage;

public sealed class SettingsWindow : Window
{
    private readonly WpfComboBox _layout;
    private readonly WpfComboBox _theme;
    private readonly WpfCheckBox _showClaude;
    private readonly WpfCheckBox _showCodex;
    private readonly TextBlock _providerValidation;
    private readonly WpfComboBox _monitor;
    private readonly WpfComboBox _position;
    private readonly WpfComboBox _interval;
    private readonly WpfTextBox _offset;
    private readonly WpfCheckBox _hideInFullscreen;
    private readonly AppSettings _initial;
    private readonly Action<AppSettings> _save;

    public SettingsWindow(AppSettings settings, Action<AppSettings> save)
    {
        _initial = settings;
        _save = save;

        Title = "AI Taskbar Usage – Settings";
        Width = 500;
        Height = 660;
        MinWidth = 430;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(MediaColor.FromRgb(245, 246, 249));

        var root = new StackPanel { Margin = new Thickness(24) };
        root.Children.Add(new TextBlock
        {
            Text = "Taskbar monitor",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(MediaColor.FromRgb(26, 31, 42))
        });
        root.Children.Add(new TextBlock
        {
            Text = "Choose what appears and how the taskbar widget behaves.",
            Foreground = WpfBrushes.DimGray,
            Margin = new Thickness(0, 4, 0, 18)
        });

        var providerChoices = new StackPanel { Orientation = WpfOrientation.Horizontal };
        _showClaude = ProviderCheckBox("Claude Code", "Anthropic usage", settings.ShowClaude, MediaColor.FromRgb(232, 151, 82));
        _showCodex = ProviderCheckBox("OpenAI Codex", "ChatGPT usage", settings.ShowCodex, MediaColor.FromRgb(110, 176, 255));
        providerChoices.Children.Add(_showClaude);
        providerChoices.Children.Add(_showCodex);

        _providerValidation = new TextBlock
        {
            Text = "Select at least one AI.",
            Foreground = new SolidColorBrush(MediaColor.FromRgb(190, 58, 68)),
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0),
            Visibility = Visibility.Collapsed
        };
        _showClaude.Checked += (_, _) => _providerValidation.Visibility = Visibility.Collapsed;
        _showCodex.Checked += (_, _) => _providerValidation.Visibility = Visibility.Collapsed;

        var providerPanel = new StackPanel();
        providerPanel.Children.Add(new TextBlock
        {
            Text = "Displayed AIs",
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Foreground = new SolidColorBrush(MediaColor.FromRgb(35, 40, 52)),
            Margin = new Thickness(0, 0, 0, 10)
        });
        providerPanel.Children.Add(providerChoices);
        providerPanel.Children.Add(_providerValidation);
        root.Children.Add(new Border
        {
            Background = WpfBrushes.White,
            BorderBrush = new SolidColorBrush(MediaColor.FromRgb(220, 224, 232)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 20),
            Child = providerPanel
        });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _layout = ComboBox(new[]
        {
            (LayoutMode.Detailed, "Detailed"),
            (LayoutMode.Compact, "Compact")
        }, settings.Layout);
        AddRow(grid, 0, "Layout", _layout);

        _theme = ComboBox(new[]
        {
            (ThemeMode.System, "System"),
            (ThemeMode.Dark, "Dark"),
            (ThemeMode.Light, "Light")
        }, settings.Theme);
        AddRow(grid, 1, "Theme", _theme);

        _monitor = ComboBox(BuildMonitorOptions(settings.MonitorSelection), settings.MonitorSelection);
        AddRow(grid, 2, "Display", _monitor);

        _position = ComboBox(new[]
        {
            (TaskbarPosition.BeforeNotificationArea, "Before notification area"),
            (TaskbarPosition.Left, "Left"),
            (TaskbarPosition.Center, "Center"),
            (TaskbarPosition.Right, "Right")
        }, settings.Position);
        AddRow(grid, 3, "Taskbar position", _position);

        _offset = new WpfTextBox
        {
            Text = settings.PositionOffsetDip.ToString(CultureInfo.InvariantCulture),
            Padding = new Thickness(8, 5, 8, 5),
            MinWidth = 90
        };
        AddRow(grid, 4, "Position offset", _offset);

        _interval = ComboBox(new[]
        {
            (30, "30 seconds"),
            (60, "60 seconds"),
            (120, "2 minutes"),
            (180, "3 minutes (recommended for Claude)"),
            (300, "5 minutes")
        }, settings.RefreshIntervalSeconds);
        AddRow(grid, 5, "Refresh interval", _interval);

        _hideInFullscreen = new WpfCheckBox
        {
            Content = "Hide while a fullscreen app is active",
            IsChecked = settings.HideInFullscreen,
            Margin = new Thickness(0, 10, 0, 0)
        };
        Grid.SetColumn(_hideInFullscreen, 1);
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_hideInFullscreen, 6);
        grid.Children.Add(_hideInFullscreen);

        root.Children.Add(grid);
        root.Children.Add(new TextBlock
        {
            Text = "Offset is measured in device-independent pixels. Positive values move the widget to the right. The default position avoids the Windows notification area.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = WpfBrushes.DimGray,
            Margin = new Thickness(170, 14, 0, 0)
        });

        var buttons = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 26, 0, 0)
        };
        var cancel = new WpfButton { Content = "Cancel", Padding = new Thickness(16, 7, 16, 7), Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => Close();
        var saveButton = new WpfButton { Content = "Save", Padding = new Thickness(18, 7, 18, 7), IsDefault = true };
        saveButton.Click += (_, _) => SaveSettings();
        buttons.Children.Add(cancel);
        buttons.Children.Add(saveButton);
        root.Children.Add(buttons);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = root
        };
    }

    private void SaveSettings()
    {
        if (_showClaude.IsChecked != true && _showCodex.IsChecked != true)
        {
            _providerValidation.Visibility = Visibility.Visible;
            return;
        }

        var offset = 0;
        _ = int.TryParse(_offset.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out offset);
        var selectedLayout = SelectedValue(_layout, _initial.Layout);
        var selectedTheme = SelectedValue(_theme, _initial.Theme);
        var selectedMonitor = SelectedValue(_monitor, _initial.MonitorSelection);
        var selectedPosition = SelectedValue(_position, _initial.Position);
        var selectedInterval = SelectedValue(_interval, _initial.RefreshIntervalSeconds);

        _save(_initial with
        {
            Layout = selectedLayout,
            Theme = selectedTheme,
            ShowClaude = _showClaude.IsChecked == true,
            ShowCodex = _showCodex.IsChecked == true,
            MonitorSelection = selectedMonitor,
            Position = selectedPosition,
            PositionOffsetDip = Math.Clamp(offset, -1000, 1000),
            RefreshIntervalSeconds = selectedInterval,
            HideInFullscreen = _hideInFullscreen.IsChecked == true
        });
        Close();
    }

    private static WpfCheckBox ProviderCheckBox(string title, string subtitle, bool isChecked, MediaColor accent)
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(accent)
        });
        content.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 11,
            Foreground = WpfBrushes.DimGray
        });

        return new WpfCheckBox
        {
            Content = content,
            IsChecked = isChecked,
            MinWidth = 190,
            Padding = new Thickness(4),
            VerticalContentAlignment = VerticalAlignment.Center
        };
    }

    private static (string Value, string Label)[] BuildMonitorOptions(string selected)
    {
        var options = new List<(string Value, string Label)>
        {
            ("Primary", "Primary display (recommended)")
        };

        foreach (var screen in WinFormsScreen.AllScreens.OrderBy(screen => screen.DeviceName, StringComparer.OrdinalIgnoreCase))
        {
            var name = screen.DeviceName.Replace(@"\\.\", string.Empty, StringComparison.Ordinal);
            var primary = screen.Primary ? " · primary" : string.Empty;
            options.Add((screen.DeviceName, $"{name} · {screen.Bounds.Width} × {screen.Bounds.Height}{primary}"));
        }

        if (!string.Equals(selected, "Primary", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(selected, "All", StringComparison.OrdinalIgnoreCase) &&
            options.All(option => !string.Equals(option.Value, selected, StringComparison.OrdinalIgnoreCase)))
        {
            options.Add((selected, $"{selected} · currently unavailable"));
        }

        options.Add(("All", "All displays"));
        return options.ToArray();
    }

    private static WpfComboBox ComboBox<T>((T Value, string Label)[] values, T selected)
    {
        var combo = new WpfComboBox { Padding = new Thickness(6, 3, 6, 3), MinWidth = 230 };
        foreach (var (value, label) in values)
        {
            combo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }

        combo.SelectedIndex = Array.FindIndex(values, item => EqualityComparer<T>.Default.Equals(item.Value, selected));
        if (combo.SelectedIndex < 0)
        {
            combo.SelectedIndex = 0;
        }

        return combo;
    }

    private static T SelectedValue<T>(WpfComboBox combo, T fallback)
    {
        return combo.SelectedItem is WpfComboBoxItem { Tag: T value } ? value : fallback;
    }

    private static void AddRow(Grid grid, int row, string label, WpfControl control)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var text = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 12),
            Foreground = new SolidColorBrush(MediaColor.FromRgb(47, 53, 66))
        };
        Grid.SetRow(text, row);
        grid.Children.Add(text);
        Grid.SetColumn(control, 1);
        Grid.SetRow(control, row);
        control.Margin = new Thickness(0, 0, 0, 12);
        grid.Children.Add(control);
    }
}
