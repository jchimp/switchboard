using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Switchboard.Core.WindowsTerminal;

/// <summary>Outcome of an appearance import: the new host settings text, or why there is none.</summary>
public sealed record WtAppearanceImportResult(string? Json, int ProfilesMatched, string? Error)
{
    public bool Succeeded => Json is not null;
}

/// <summary>
/// Copies the look of the installed Windows Terminal into the terminal host's settings
/// (ADR-0022). Pure text in, text out — the App layer finds, backs up and writes the files
/// (invariant 1). Only appearance moves: command lines, starting folders, the profile list,
/// key bindings and window/startup behaviour never do, because those decide what a
/// Switchboard terminal runs and how the host behaves, not how it looks. Both inputs are
/// user-editable JSONC, so nothing here throws on bad input.
/// </summary>
public static class WtAppearanceImport
{
    /// <summary>
    /// Profile keys that only change how a terminal looks. <c>tabColor</c> is deliberately
    /// absent: Switchboard passes each terminal's color with <c>--tabColor</c> (R9).
    /// </summary>
    public static readonly IReadOnlySet<string> ProfileAppearanceKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "font", "fontFace", "fontSize", "fontWeight",
        "colorScheme", "foreground", "background", "cursorColor", "selectionBackground",
        "cursorShape", "cursorHeight", "intenseTextStyle", "adjustIndistinguishableColors",
        "opacity", "useAcrylic", "acrylicOpacity", "padding", "antialiasingMode", "scrollbarState",
        "backgroundImage", "backgroundImageOpacity", "backgroundImageStretchMode", "backgroundImageAlignment",
        "unfocusedAppearance", "experimental.retroTerminalEffect", "experimental.pixelShaderPath",
        "experimental.pixelShaderImagePath", "builtinGlyphs", "colorGlyphs",
    };

    /// <summary>Root keys that set the window's look, copied as the installed WT has them.</summary>
    public static readonly IReadOnlyList<string> RootAppearanceKeys =
        ["theme", "initialCols", "initialRows", "useAcrylicInTabRow"];

    /// <summary>Root arrays merged by each entry's <c>name</c>; the installed WT wins a clash.</summary>
    public static readonly IReadOnlyList<string> NamedCollections = ["schemes", "themes"];

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // Relaxed escaping: the output is the user's own settings file, not HTML. The default
    // encoder escapes every quote, ampersand, apostrophe and non-ASCII name in the whole file,
    // which WT reads fine but nobody can edit by hand.
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// True when the host has no appearance defaults of its own but the installed WT does:
    /// a fresh portable copy nobody has styled yet. The settings page offers the import then.
    /// </summary>
    public static bool HostLooksUnstyled(string installedJson, string hostJson)
    {
        try
        {
            return Parse(installedJson) is JsonObject installed && Parse(hostJson) is JsonObject host
                && AppearanceKeyCount(Defaults(installed)) > 0
                && AppearanceKeyCount(Defaults(host)) == 0;
        }
        catch (Exception ex) when (IsBadShape(ex))
        {
            return false;
        }
    }

    /// <summary>
    /// The host settings with the installed WT's appearance applied. For <c>profiles.defaults</c>
    /// and every host profile matched by guid (then name), the appearance keys become exactly
    /// the installed WT's — keys it lacks are removed, so a re-import gives the same result.
    /// Host profiles with no match are left alone.
    /// </summary>
    public static WtAppearanceImportResult Merge(string installedJson, string hostJson)
    {
        try
        {
            return MergeCore(installedJson, hostJson);
        }
        catch (Exception ex) when (IsBadShape(ex))
        {
            return new(null, 0, $"A settings.json has a shape the import cannot read ({ex.Message}).");
        }
    }

    /// <summary>
    /// What hostile-but-parseable JSON throws later, on first read: a duplicate key (WT itself
    /// lets the last one win, so hand-edited files can have them) or a node of the wrong kind.
    /// </summary>
    private static bool IsBadShape(Exception ex) => ex is ArgumentException or InvalidOperationException;

    private static WtAppearanceImportResult MergeCore(string installedJson, string hostJson)
    {
        if (Parse(installedJson) is not JsonObject installed)
        {
            return new(null, 0, "The installed Windows Terminal's settings.json could not be read.");
        }

        if (Parse(hostJson) is not JsonObject host)
        {
            return new(null, 0, "The terminal host's settings.json could not be read.");
        }

        foreach (var key in RootAppearanceKeys)
        {
            CopyOrRemove(installed, host, key);
        }

        foreach (var key in NamedCollections)
        {
            MergeByName(installed, host, key);
        }

        // No defaults in the installed WT means its default look; "exactly the installed WT's"
        // then clears the host's appearance defaults too.
        var hostProfiles = EnsureProfilesObject(host);
        if (hostProfiles["defaults"] is not JsonObject hostDefaults)
        {
            hostDefaults = new JsonObject();
            hostProfiles["defaults"] = hostDefaults;
        }

        ReplaceAppearance(Defaults(installed) ?? new JsonObject(), hostDefaults);

        var matched = 0;
        var installedList = ProfileList(installed);
        foreach (var hostProfile in ProfileList(host))
        {
            if (FindMatch(installedList, hostProfile) is { } source)
            {
                ReplaceAppearance(source, hostProfile);
                matched++;
            }
        }

        return new(host.ToJsonString(WriteOptions), matched, null);
    }

    private static JsonNode? Parse(string json)
    {
        try
        {
            return JsonNode.Parse(json, documentOptions: ReadOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonObject? Defaults(JsonNode root) =>
        root["profiles"] is JsonObject profiles ? profiles["defaults"] as JsonObject : null;

    private static int AppearanceKeyCount(JsonObject? obj) =>
        obj?.Count(p => ProfileAppearanceKeys.Contains(p.Key)) ?? 0;

    /// <summary>WT allows <c>profiles</c> as a bare array; defaults need the object shape.</summary>
    private static JsonObject EnsureProfilesObject(JsonObject root)
    {
        switch (root["profiles"])
        {
            case JsonObject existing:
                return existing;
            case JsonArray bare:
                root.Remove("profiles");
                var wrapped = new JsonObject { ["list"] = bare };
                root["profiles"] = wrapped;
                return wrapped;
            default:
                var created = new JsonObject { ["list"] = new JsonArray() };
                root["profiles"] = created;
                return created;
        }
    }

    private static List<JsonObject> ProfileList(JsonNode root)
    {
        var list = root["profiles"] switch
        {
            JsonArray bare => bare,
            JsonObject obj => obj["list"] as JsonArray,
            _ => null,
        };
        return list?.OfType<JsonObject>().ToList() ?? [];
    }

    private static JsonObject? FindMatch(List<JsonObject> candidates, JsonObject profile)
    {
        if (Text(profile, "guid") is { } guid
            && candidates.FirstOrDefault(c => string.Equals(Text(c, "guid"), guid, StringComparison.OrdinalIgnoreCase)) is { } byGuid)
        {
            return byGuid;
        }

        return Text(profile, "name") is { } name
            ? candidates.FirstOrDefault(c => string.Equals(Text(c, "name"), name, StringComparison.Ordinal))
            : null;
    }

    private static string? Text(JsonObject obj, string key) =>
        obj[key] is JsonValue value && value.TryGetValue<string>(out var s) && s.Length > 0 ? s : null;

    private static void ReplaceAppearance(JsonObject source, JsonObject target)
    {
        foreach (var key in target.Select(p => p.Key).Where(ProfileAppearanceKeys.Contains).ToList())
        {
            target.Remove(key);
        }

        foreach (var (key, value) in source)
        {
            if (ProfileAppearanceKeys.Contains(key))
            {
                target[key] = value?.DeepClone();
            }
        }
    }

    private static void CopyOrRemove(JsonObject source, JsonObject target, string key)
    {
        target.Remove(key);
        if (source.TryGetPropertyValue(key, out var value))
        {
            target[key] = value?.DeepClone();
        }
    }

    private static void MergeByName(JsonObject source, JsonObject target, string key)
    {
        if (source[key] is not JsonArray incoming)
        {
            return;
        }

        if (target[key] is not JsonArray existing)
        {
            existing = new JsonArray();
            target[key] = existing;
        }

        foreach (var entry in incoming.OfType<JsonObject>())
        {
            // Nameless entries can't be referenced by WT, and would be appended again on every
            // import (nothing to match them by), so they are skipped.
            if (Text(entry, "name") is not { } name)
            {
                continue;
            }

            var clash = existing.OfType<JsonObject>().FirstOrDefault(e => Text(e, "name") == name);
            if (clash is not null)
            {
                existing.Remove(clash);
            }

            existing.Add(entry.DeepClone());
        }
    }
}
