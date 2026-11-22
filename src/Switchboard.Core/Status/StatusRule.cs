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

    /// <summary>
    /// Checks a pattern the way <see cref="TitleRuleEvaluator"/> will compile it, so the
    /// settings editor can reject a bad regex before it reaches the file. Returns the
    /// failure message, or null when the pattern is usable.
    /// </summary>
    /// <remarks>
    /// The evaluator treats an uncompilable pattern as "never matches" (invariant 9: user
    /// regexes are hostile input and must never crash the tracker). That is the right
    /// runtime behaviour and the wrong editing experience — silently dead rules look like
    /// a Switchboard bug — so the editor validates up front instead.
    /// </remarks>
    public static string? ValidatePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return "Pattern is empty.";
        }

        try
        {
            _ = new Regex(pattern, TitleRuleEvaluator.Options, TitleRuleEvaluator.MatchTimeout);
            return null;
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }
    }
}

/// <summary>
/// Compiles rules once and evaluates a title against them, terminal rules first, then
/// global rules, first match wins. Returns null when nothing matched so the caller
/// can decide whether to keep the previous state.
/// </summary>
public sealed class TitleRuleEvaluator
{
    internal const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    internal static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(20);

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
            var regex = new Regex(pattern, Options, MatchTimeout);
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
