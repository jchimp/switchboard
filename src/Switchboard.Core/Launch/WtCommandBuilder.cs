using System.Diagnostics;
using Switchboard.Core.Registry;

namespace Switchboard.Core.Launch;

/// <summary>
/// Builds the <c>wt.exe</c> invocation for a definition. Uses
/// <see cref="ProcessStartInfo.ArgumentList"/> so quoting is handled by the runtime,
/// never by string concatenation.
/// </summary>
/// <remarks>
/// Shape: <c>wt -w &lt;name&gt; new-tab -p &lt;profile&gt; [-d &lt;cwd&gt;] --title &lt;name&gt;
/// --tabColor &lt;color&gt; [--suppressApplicationTitle] [-- &lt;shell wrapper&gt;]</c>.
/// The <c>--</c> tail replaces the profile's own command line, which is why a
/// <see cref="TerminalDefinition.Command"/> has to be wrapped in the right shell
/// (ADR-0004).
/// </remarks>
public static class WtCommandBuilder
{
    public const string Executable = "wt.exe";

    public static ProcessStartInfo Build(TerminalDefinition def)
    {
        if (def.Validate() is { } error)
        {
            throw new ArgumentException(error, nameof(def));
        }

        var psi = new ProcessStartInfo
        {
            FileName = Executable,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var a = psi.ArgumentList;
        a.Add("-w");
        a.Add(def.Name);
        a.Add("new-tab");
        a.Add("-p");
        a.Add(def.Profile);

        if (!string.IsNullOrWhiteSpace(def.WorkingDirectory) && def.Shell != ShellKind.Wsl)
        {
            // WSL gets its cwd via --cd in the wrapper; wt -d expects a Windows path.
            a.Add("-d");
            a.Add(def.WorkingDirectory);
        }

        a.Add("--title");
        a.Add(def.Name);
        a.Add("--tabColor");
        a.Add(def.Color);

        if (def.LockTitle)
        {
            a.Add("--suppressApplicationTitle");
        }

        foreach (var arg in ShellWrapper(def))
        {
            a.Add(arg);
        }

        return psi;
    }

    /// <summary>
    /// The <c>-- ...</c> tail, or nothing when the profile's default command line should run.
    /// Every wrapper leaves an interactive shell behind so the window survives the command.
    /// </summary>
    internal static IReadOnlyList<string> ShellWrapper(TerminalDefinition def)
    {
        var hasCommand = !string.IsNullOrWhiteSpace(def.Command);
        var isWsl = def.Shell == ShellKind.Wsl;

        if (!hasCommand && !(isWsl && !string.IsNullOrWhiteSpace(def.WorkingDirectory)))
        {
            return [];
        }

        return def.Shell switch
        {
            ShellKind.Pwsh => ["--", "pwsh.exe", "-NoLogo", "-NoExit", "-Command", def.Command!],
            ShellKind.WindowsPowerShell => ["--", "powershell.exe", "-NoLogo", "-NoExit", "-Command", def.Command!],
            ShellKind.Cmd => ["--", "cmd.exe", "/k", def.Command!],
            ShellKind.Wsl => WslWrapper(def),
            _ => throw new ArgumentOutOfRangeException(nameof(def), def.Shell, "Unknown shell kind."),
        };
    }

    private static string[] WslWrapper(TerminalDefinition def)
    {
        var args = new List<string> { "--", "wsl.exe" };
        if (!string.IsNullOrWhiteSpace(def.Distro))
        {
            args.Add("-d");
            args.Add(def.Distro);
        }

        if (!string.IsNullOrWhiteSpace(def.WorkingDirectory))
        {
            args.Add("--cd");
            args.Add(def.WorkingDirectory);
        }

        if (!string.IsNullOrWhiteSpace(def.Command))
        {
            // -l so PATH/.profile apply (nvm, uv, claude live there); exec keeps the shell alive.
            args.Add("--");
            args.Add("bash");
            args.Add("-lic");
            args.Add($"{def.Command}; exec bash -l");
        }

        return [.. args];
    }

    /// <summary>Human-readable rendering for logs and the settings UI. Not for execution.</summary>
    public static string Render(ProcessStartInfo psi) =>
        psi.FileName + " " + string.Join(' ', psi.ArgumentList.Select(x => x.Contains(' ') ? $"\"{x}\"" : x));
}
