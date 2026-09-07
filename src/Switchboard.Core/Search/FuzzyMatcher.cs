namespace Switchboard.Core.Search;

/// <summary>
/// Case-insensitive subsequence match for the switcher's filter box. Every query character
/// must appear in the candidate in order; the score prefers matches at the start, after a
/// word boundary, and in consecutive runs. Spans drive highlight bolding in the card list.
/// </summary>
public static class FuzzyMatcher
{
    private const int WordStartBonus = 10;
    private const int ConsecutiveBonus = 5;
    // Steep enough that a tight consecutive run beats a scatter of word-boundary hits.
    private const int GapPenalty = 3;

    /// <summary>True when every query character appears in order. An empty query matches everything.</summary>
    public static bool TryMatch(string query, string candidate, out FuzzyMatch match)
    {
        var indexes = new List<int>(query.Length);
        var score = 0;
        var at = 0;
        foreach (var q in query)
        {
            var found = -1;
            for (var i = at; i < candidate.Length; i++)
            {
                if (char.ToLowerInvariant(candidate[i]) == char.ToLowerInvariant(q))
                {
                    found = i;
                    break;
                }
            }

            if (found < 0)
            {
                match = FuzzyMatch.None;
                return false;
            }

            if (found == 0 || !char.IsLetterOrDigit(candidate[found - 1]))
            {
                score += WordStartBonus;
            }

            if (indexes.Count > 0 && found == indexes[^1] + 1)
            {
                score += ConsecutiveBonus;
            }

            score -= GapPenalty * (found - at);
            indexes.Add(found);
            at = found + 1;
        }

        match = new FuzzyMatch(score, CollapseToSpans(indexes));
        return true;
    }

    private static IReadOnlyList<Range> CollapseToSpans(List<int> indexes)
    {
        var spans = new List<Range>();
        for (var i = 0; i < indexes.Count; i++)
        {
            var start = indexes[i];
            while (i + 1 < indexes.Count && indexes[i + 1] == indexes[i] + 1)
            {
                i++;
            }

            spans.Add(new Range(start, indexes[i] + 1));
        }

        return spans;
    }
}

/// <summary>A successful match: higher <paramref name="Score"/> ranks first; <paramref name="Spans"/> cover the matched characters.</summary>
public sealed record FuzzyMatch(int Score, IReadOnlyList<Range> Spans)
{
    public static readonly FuzzyMatch None = new(int.MinValue, []);
}
