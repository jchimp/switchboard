namespace Switchboard.Core.Registry;

public interface IRegistryStore
{
    /// <summary>Loads the registry, or returns <see cref="RegistryFile.Empty"/> when none exists yet.</summary>
    Task<RegistryFile> LoadAsync(CancellationToken ct = default);

    /// <summary>Atomically replaces the registry on disk, keeping one <c>.bak</c>.</summary>
    Task SaveAsync(RegistryFile file, CancellationToken ct = default);
}
