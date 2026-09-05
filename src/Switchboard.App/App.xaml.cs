using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Switchboard.App.Services;
using Switchboard.App.Strip;
using Switchboard.App.ViewModels;
using Switchboard.Core.Launch;
using Switchboard.Core.Registry;
using Switchboard.Windows;
using Switchboard.Windows.Hotkeys;
using Switchboard.Windows.Tracking;

namespace Switchboard.App;

/// <summary>Composition root. Wires the graph, loads the registry, shows the strip, registers hotkeys.</summary>
public partial class App : Application
{
    private ServiceProvider? _services;
    private Mutex? _instanceMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // One strip per desktop session: a second RegisterHotKey set would fail wholesale,
        // so a duplicate launch (pinned shortcut, autostart) just exits quietly.
        _instanceMutex = new Mutex(initiallyOwned: true, @"Local\Switchboard.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            _instanceMutex.Dispose();
            _instanceMutex = null;
            Shutdown(0);
            return;
        }

        var logDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Switchboard", "logs");

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug().AddProvider(new FileLoggerProvider(logDir)).SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IRegistryStore>(_ => new JsonRegistryStore(JsonRegistryStore.DefaultPath));
        services.AddSingleton(sp => new RegistryWatcher(JsonRegistryStore.DefaultPath, sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ITerminalLauncher, ProcessTerminalLauncher>();
        services.AddSingleton<FrameColorizer>();
        services.AddSingleton<TaskbarVisibility>();
        services.AddSingleton<AppBar>();
        services.AddSingleton<WindowSampler>();
        services.AddSingleton<WindowTracker>();
        services.AddSingleton<GlobalHotkeyManager>();
        services.AddSingleton<TerminalSession>();
        services.AddSingleton<QuickLaunchService>();
        services.AddSingleton<HotkeyBindings>();
        services.AddSingleton<StripViewModel>();
        services.AddSingleton<StripWindow>();
        services.AddSingleton<RailViewModel>();
        services.AddSingleton<Rail.RailWindow>();
        _services = services.BuildServiceProvider();

        var log = _services.GetRequiredService<ILogger<App>>();
        DispatcherUnhandledException += (_, args) =>
        {
            log.LogError(args.Exception, "Unhandled exception on the UI thread");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            log.LogCritical(args.ExceptionObject as Exception, "Unhandled exception off the UI thread");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            log.LogError(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };

        try
        {
            var session = _services.GetRequiredService<TerminalSession>();
            await session.InitializeAsync();

            var strip = _services.GetRequiredService<StripWindow>();
            strip.Show();

            // PrintWindow sampling would stutter the rail's 16 ms slide frames (ADR-0013).
            var rail = _services.GetRequiredService<Rail.RailWindow>();
            session.MotionSamplingPaused = () => rail.IsSliding;

            var bindings = _services.GetRequiredService<HotkeyBindings>();
            bindings.Attach(strip);
            session.RegistryChanged += bindings.Rebind;
            strip.TaskbarRecreated += session.OnTaskbarRecreated;
            log.LogInformation("Switchboard up: {Count} terminals in registry", session.Registry.Terminals.Count);
        }
        catch (Exception ex)
        {
            log.LogCritical(ex, "Startup failed");
            MessageBox.Show(ex.Message, "Switchboard could not start", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        if (_instanceMutex is not null)
        {
            _instanceMutex.ReleaseMutex();
            _instanceMutex.Dispose();
        }

        base.OnExit(e);
    }
}
