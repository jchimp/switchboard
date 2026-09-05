using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Switchboard.Core.Launch;
using Switchboard.Core.Registry;

namespace Switchboard.Windows;

/// <summary>Runs the <see cref="WtCommandBuilder"/> output. Nothing more; the tracker handles what happens next.</summary>
public sealed class ProcessTerminalLauncher : ITerminalLauncher
{
    private readonly ILogger<ProcessTerminalLauncher> _log;

    public ProcessTerminalLauncher(ILogger<ProcessTerminalLauncher> log)
    {
        _log = log;
    }

    public Task LaunchAsync(TerminalDefinition def, CancellationToken ct = default)
    {
        var psi = WtCommandBuilder.Build(def);
        _log.LogInformation("Launching {Name}: {Command}", def.Name, WtCommandBuilder.Render(psi));

        try
        {
            using var _ = Process.Start(psi)
                ?? throw new InvalidOperationException("Process.Start returned null; is wt.exe on PATH?");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException("Could not start wt.exe. Install Windows Terminal from the Store or winget.", ex);
        }

        return Task.CompletedTask;
    }
}
