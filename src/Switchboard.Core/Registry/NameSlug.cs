using System.Text;

namespace Switchboard.Core.Registry;

/// <summary>
/// Turns a Windows Terminal window title into a registry name (R27). Import proposes one of
/// these per unbound window so the user can accept a whole batch without typing.
/// </summary>
/// <remarks>
/// Names are load-bearing: the name is the <c>wt -w</c> window name and the initial title
/// (invariant 4), and <see cref="TerminalDefinition.Validate"/> only accepts
/// <c>^[a-z0-9][a-z0-9-]{0,31}$</c>. So this never returns something that would fail
/// validation — when a title has nothing usable in it, the quick-launch prefix takes over.
/// </remarks>
public static class NameSlug
{
    /// <summary>Matches TerminalDefinition's name rule.</summary>
    public const int MaxLength = 32;

    /// <summary>
    /// Best available name for <paramref name="title"/> that is not already in
    /// <paramref name="taken"/>. Falls back to <paramref name="prefix"/> plus a number.
    /// </summary>
    public static string FromTitle(string? title, string prefix, IReadOnlySet<string> taken)
    {
        var slug = Slugify(title);
        if (slug.Length == 0)
        {
            slug = Slugify(prefix) is { Length: > 0 } p ? p : "term";
        }

        return Deduplicate(slug, taken);
    }

    /// <summary>Lower-cases, replaces runs of anything else with a single hyphen, and trims.</summary>
    public static string Slugify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        // A leading digit is legal, a leading hyphen is not; trailing hyphens are just noise.
        var slug = builder.ToString().Trim('-');
        return slug.Length > MaxLength ? slug[..MaxLength].TrimEnd('-') : slug;
    }

    private static string Deduplicate(string slug, IReadOnlySet<string> taken)
    {
        if (!taken.Contains(slug))
        {
            return slug;
        }

        for (var n = 2; ; n++)
        {
            var suffix = $"-{n}";
            var stem = slug.Length + suffix.Length > MaxLength
                ? slug[..(MaxLength - suffix.Length)].TrimEnd('-')
                : slug;
            var candidate = stem + suffix;
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
