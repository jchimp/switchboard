using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Switchboard.Core.Launch;
using Switchboard.Core.Registry;

namespace Switchboard.Windows;

/// <summary>Runs the <see cref="WtCommandBuilder"/> output. Nothing more; the tracker handles what happens next.</summary>
public sealed class ProcessTerminalLauncher : ITerminalLauncher
{
    private readonly ILogger<ProcessTerminalLauncher> _log;
    private readonly TerminalHost _host;

    public ProcessTerminalLauncher(ILogger<ProcessTerminalLauncher> log, TerminalHost host)
    {
        _log = log;
        _host = host;
    }

    public Task LaunchAsync(TerminalDefinition def, CancellationToken ct = default)
    {
        // Checked here, not left to Process.Start: its error would only say "file not found".
        if (_host.IsMissing)
        {
            throw new InvalidOperationException(
                $"No wt.exe in the terminal host folder '{_host.Folder}'. Fix it in Settings → Terminals, " +
                "or clear it to use the installed Windows Terminal.");
        }

        var psi = WtCommandBuilder.Build(def, _host.WtExecutable);
        _log.LogInformation("Launching {Name}: {Command}", def.Name, WtCommandBuilder.Render(psi));

        try
        {
            using var _ = Process.Start(psi)
                ?? throw new InvalidOperationException($"Process.Start returned null for {psi.FileName}.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not start {psi.FileName}. Install Windows Terminal, or set a terminal host folder.", ex);
        }

        return Task.CompletedTask;
    }
}
