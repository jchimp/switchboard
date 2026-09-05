using System.Text.RegularExpressions;

namespace Switchboard.Core.Status;

/// <summary>A title regex that maps to a state. User-supplied, so evaluated with a timeout.</summary>
public sealed record StatusRule
{
    public required TerminalState State { get; init; }

    public required string Pattern { get; init; }

    public static readonly IReadOnlyList<StatusRule> Defaults =
    [
        new() { State = TerminalState.Attention, Pattern = @"\b(waiting|done|finished|error|failed|traceback)\b" },
        new() { State = TerminalState.Working, Pattern = @"\b(running|building|thinking|installing|compiling|hmr update|reloading)\b" },
    ];
}

/// <summary>
/// Compiles rules once and evaluates a title against them, terminal rules first, then
/// global rules, first match wins. Returns null when nothing matched so the caller
/// can decide whether to keep the previous state.
/// </summary>
public sealed class TitleRuleEvaluator
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(20);

    private readonly Dictionary<string, Regex?> _cache = new(StringComparer.Ordinal);

    public TerminalState? Evaluate(string title, IReadOnlyList<StatusRule> terminalRules, IReadOnlyList<StatusRule> globalRules)
    {
        if (string.IsNullOrEmpty(title))
        {
            return null;
        }

        foreach (var rule in terminalRules.Concat(globalRules))
        {
            if (Compile(rule.Pattern) is { } regex && SafeIsMatch(regex, title))
            {
                return rule.State;
            }
        }

        return null;
    }

    private Regex? Compile(string pattern)
    {
        if (_cache.TryGetValue(pattern, out var cached))
        {
            return cached;
        }

        try
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
            _cache[pattern] = regex;
            return regex;
        }
        catch (ArgumentException)
        {
            // Bad user regex: treat as "never matches" rather than crashing the tracker.
            _cache[pattern] = null;
            return null;
        }
    }

    private static bool SafeIsMatch(Regex regex, string input)
    {
        try
        {
            return regex.IsMatch(input);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
