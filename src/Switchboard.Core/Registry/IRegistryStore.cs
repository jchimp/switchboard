namespace Switchboard.Core.Registry;

public interface IRegistryStore
{
    /// <summary>Loads the registry, or returns <see cref="RegistryFile.Empty"/> when none exists yet.</summary>
    Task<RegistryFile> LoadAsync(CancellationToken ct = default);

    /// <summary>Atomically replaces the registry on disk, keeping one <c>.bak</c>.</summary>
    Task SaveAsync(RegistryFile file, CancellationToken ct = default);

    /// <summary>
    /// True when the last load fell back to a legacy file name because the current one was
    /// absent. Cleared by the next save, which always writes the current name.
    /// </summary>
    bool AdoptedLegacyFile { get; }

    /// <summary>
    /// True when the last load found no registry file at all, current or legacy: a first run.
    /// Cleared by the next save.
    /// </summary>
    bool FoundNoFile { get; }
}
