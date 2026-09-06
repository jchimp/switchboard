using System.Text.Json;
using System.Text.Json.Serialization;

namespace Switchboard.Core.Registry;

/// <summary>
/// JSON-on-disk registry with atomic writes. The store knows nothing about where the file
/// lives: the composition root supplies the path (<c>RegistryPaths</c> in the app, a temp
/// path in tests), so Core stays platform-neutral (invariant 1).
/// </summary>
public sealed class JsonRegistryStore : IRegistryStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <param name="path">Where the registry is read from and always written to.</param>
    /// <param name="legacyPath">
    /// An older file name to fall back to for reading when <paramref name="path"/> does not
    /// exist yet. Injected rather than hard-coded so Core stays platform-neutral.
    /// </param>
    public JsonRegistryStore(string path, string? legacyPath = null)
    {
        FilePath = path ?? throw new ArgumentNullException(nameof(path));
        LegacyPath = legacyPath;
    }

    public string FilePath { get; }

    public string? LegacyPath { get; }

    public bool AdoptedLegacyFile { get; private set; }

    public async Task<RegistryFile> LoadAsync(CancellationToken ct = default)
    {
        var source = ResolveLoadPath();
        if (source is null)
        {
            return RegistryFile.Empty;
        }

        AdoptedLegacyFile = source != FilePath;

        await using var stream = File.OpenRead(source);
        var file = await JsonSerializer.DeserializeAsync<RegistryFile>(stream, Options, ct)
                   ?? throw new InvalidDataException($"Registry at '{source}' deserialised to null.");

        if (file.Version > RegistryFile.CurrentVersion)
        {
            throw new InvalidDataException(
                $"Registry version {file.Version} is newer than this build supports ({RegistryFile.CurrentVersion}).");
        }

        var errors = file.Terminals.Select(t => t.Validate()).Where(e => e is not null).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidDataException("Registry has invalid terminals:\n" + string.Join('\n', errors));
        }

        var dupes = file.Terminals.GroupBy(t => t.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (dupes.Count > 0)
        {
            throw new InvalidDataException("Registry has duplicate terminal names: " + string.Join(", ", dupes));
        }

        return Migrate(file);
    }

    /// <summary>The file to read: the current name if present, else the legacy one, else nothing.</summary>
    private string? ResolveLoadPath()
    {
        if (File.Exists(FilePath))
        {
            return FilePath;
        }

        return LegacyPath is not null && File.Exists(LegacyPath) ? LegacyPath : null;
    }

    public async Task SaveAsync(RegistryFile file, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var tmp = FilePath + ".tmp";
            await using (var stream = File.Create(tmp))
            {
                await JsonSerializer.SerializeAsync(stream, file with { Version = RegistryFile.CurrentVersion }, Options, ct);
            }

            if (File.Exists(FilePath))
            {
                File.Copy(FilePath, FilePath + ".bak", overwrite: true);
            }

            File.Move(tmp, FilePath, overwrite: true);
            AdoptedLegacyFile = false;
        }
        finally
        {
            _gate.Release();
        }
    }

    // Version bumps go here. Keep each step tiny and pure.
    private static RegistryFile Migrate(RegistryFile file) => file.Version switch
    {
        RegistryFile.CurrentVersion => file,
        _ => file with { Version = RegistryFile.CurrentVersion },
    };
}
