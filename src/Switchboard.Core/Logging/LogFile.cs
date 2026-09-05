using System.Globalization;
using System.Text;

namespace Switchboard.Core.Logging;

/// <summary>
/// Naming, formatting and retention rules for the daily log files. Pure so the App-layer
/// writer stays a dumb pipe; where the files live is the App's business.
/// </summary>
public static class LogFile
{
    private const string Prefix = "switchboard-";
    private const string Extension = ".log";

    public static string FileNameFor(DateOnly day) =>
        $"{Prefix}{day:yyyyMMdd}{Extension}";

    /// <summary>One log line; exception text (if any) follows on indented lines.</summary>
    public static string FormatLine(DateTimeOffset timestamp, string level, string category, string message, string? exception = null)
    {
        var sb = new StringBuilder();
        sb.Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        sb.Append(" [").Append(level).Append("] ");
        sb.Append(category).Append(": ").Append(message);
        if (!string.IsNullOrEmpty(exception))
        {
            foreach (var line in exception.Split('\n'))
            {
                sb.AppendLine().Append("    ").Append(line.TrimEnd('\r'));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// File names older than <paramref name="keepDays"/> (by the date encoded in the name).
    /// Files that don't match our naming scheme are never ours to delete.
    /// </summary>
    public static IReadOnlyList<string> SelectPrunable(IEnumerable<string> fileNames, DateOnly today, int keepDays = 7)
    {
        var cutoff = today.AddDays(-(keepDays - 1));
        var prunable = new List<string>();
        foreach (var name in fileNames)
        {
            if (!name.StartsWith(Prefix, StringComparison.Ordinal) ||
                !name.EndsWith(Extension, StringComparison.Ordinal))
            {
                continue;
            }

            var stamp = name[Prefix.Length..^Extension.Length];
            if (DateOnly.TryParseExact(stamp, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                && day < cutoff)
            {
                prunable.Add(name);
            }
        }

        return prunable;
    }
}
