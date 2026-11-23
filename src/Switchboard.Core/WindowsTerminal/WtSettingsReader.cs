using System.Text.Json;

namespace Switchboard.Core.WindowsTerminal;

/// <summary>A Windows Terminal profile the picker can offer: display name, optional guid.</summary>
public sealed record WtProfile(string Name, string? Guid);

/// <summary>
/// Read-only parser for Windows Terminal's settings.json (ADR-0017). Pure — the App layer
/// resolves the file path and hands the text in (invariant 1). WT's file is JSONC and its
/// <c>profiles</c> node has two shapes (a bare array, or an object with a <c>list</c>);
/// both are user-editable hostile input, so nothing here ever throws.
/// </summary>
public static class WtSettingsReader
{
    private static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Visible profiles in file order; empty on malformed input, never an exception.</summary>
    public static IReadOnlyList<WtProfile> Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, Options);

            // TryGetProperty throws on a non-object root ("[]", "42"), which is valid JSON.
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("profiles", out var profiles))
            {
                return [];
            }

            var list = profiles.ValueKind switch
            {
                JsonValueKind.Array => profiles,
                JsonValueKind.Object when profiles.TryGetProperty("list", out var l)
                                          && l.ValueKind == JsonValueKind.Array => l,
                _ => default,
            };

            if (list.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var result = new List<WtProfile>();
            foreach (var entry in list.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object
                    || entry.TryGetProperty("hidden", out var hidden) && hidden.ValueKind == JsonValueKind.True
                    || !entry.TryGetProperty("name", out var name)
                    || name.ValueKind != JsonValueKind.String
                    || name.GetString() is not { Length: > 0 } text)
                {
                    continue;
                }

                var guid = entry.TryGetProperty("guid", out var g) && g.ValueKind == JsonValueKind.String
                    ? g.GetString()
                    : null;
                result.Add(new WtProfile(text, guid));
            }

            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
