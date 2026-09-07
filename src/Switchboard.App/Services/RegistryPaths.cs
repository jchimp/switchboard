using System.IO;

namespace Switchboard.App.Services;

/// <summary>
/// Where the registry lives on this machine. Kept out of Core, which must not know about
/// <c>%APPDATA%</c> or any other Windows shape (invariant 1); the composition root hands
/// these strings to the store and the watcher.
/// </summary>
public static class RegistryPaths
{
    private static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Switchboard");

    public static string Default => Path.Combine(Folder, "switchboard.json");

    /// <summary>The pre-rename name, still read once so existing configs survive the upgrade (ADR-0008).</summary>
    public static string Legacy => Path.Combine(Folder, "registry.json");
}
