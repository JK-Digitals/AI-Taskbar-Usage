#nullable enable
using System.Windows;

namespace AiTaskbarUsage;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstance;
    private MainController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Explorer may destroy and recreate taskbar child windows. The tray
        // process must survive that so MainController can recreate the widget.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        _singleInstance = new Mutex(true, "Local\\AI.TaskbarUsage.SingleInstance", out var created);
        if (!created)
        {
            Shutdown(2);
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            SafeLogger.Write("ui", "Unhandled dispatcher exception: " + args.Exception.GetType().Name);
            args.Handled = true;
        };

        _controller = new MainController(this);
        _controller.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
