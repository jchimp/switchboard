using System.Text.Json;
using System.Text.Json.Serialization;

namespace Switchboard.Core.Registry;

/// <summary>
/// JSON-on-disk registry with atomic writes. Default location is
/// <c>%APPDATA%\Switchboard\registry.json</c>; tests pass a temp path.
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

    public JsonRegistryStore(string path)
    {
        FilePath = path ?? throw new ArgumentNullException(nameof(path));
    }

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Switchboard", "registry.json");

    public string FilePath { get; }

    public async Task<RegistryFile> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(FilePath))
        {
            return RegistryFile.Empty;
        }

        await using var stream = File.OpenRead(FilePath);
        var file = await JsonSerializer.DeserializeAsync<RegistryFile>(stream, Options, ct)
                   ?? throw new InvalidDataException($"Registry at '{FilePath}' deserialised to null.");

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
