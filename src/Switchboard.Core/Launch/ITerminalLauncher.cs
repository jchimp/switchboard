using Switchboard.Core.Registry;

namespace Switchboard.Core.Launch;

/// <summary>Starts a terminal for a definition. The Windows layer owns the real implementation.</summary>
public interface ITerminalLauncher
{
    /// <summary>Launches and returns once <c>wt.exe</c> has been started (not once the window exists).</summary>
    Task LaunchAsync(TerminalDefinition def, CancellationToken ct = default);
}
