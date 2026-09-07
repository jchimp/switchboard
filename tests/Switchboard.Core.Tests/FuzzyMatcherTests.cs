using Switchboard.Core.Search;
using Xunit;

namespace Switchboard.Core.Tests;

public class FuzzyMatcherTests
{
    [Fact]
    public void Every_query_character_must_appear_in_order()
    {
        Assert.True(FuzzyMatcher.TryMatch("bld", "build-server", out _));
        Assert.False(FuzzyMatcher.TryMatch("dlb", "build-server", out _));
        Assert.False(FuzzyMatcher.TryMatch("buildx", "build", out _));
    }

    [Fact]
    public void Matching_is_case_insensitive()
    {
        Assert.True(FuzzyMatcher.TryMatch("API", "my-api-box", out _));
    }

    [Fact]
    public void Empty_query_matches_everything()
    {
        Assert.True(FuzzyMatcher.TryMatch("", "anything", out var match));
        Assert.Empty(match.Spans);
    }

    [Fact]
    public void Consecutive_matches_outscore_scattered_ones()
    {
        Assert.True(FuzzyMatcher.TryMatch("log", "logs", out var consecutive));
        Assert.True(FuzzyMatcher.TryMatch("log", "lab-of-gremlins", out var scattered));
        Assert.True(consecutive.Score > scattered.Score);
    }

    [Fact]
    public void A_word_boundary_match_beats_a_mid_word_match()
    {
        Assert.True(FuzzyMatcher.TryMatch("s", "web-server", out var boundary));
        Assert.True(FuzzyMatcher.TryMatch("s", "west", out var midWord));
        Assert.True(boundary.Score >= midWord.Score);
    }

    [Fact]
    public void Spans_cover_exactly_the_matched_characters()
    {
        Assert.True(FuzzyMatcher.TryMatch("bs", "build-server", out var match));
        Assert.Equal(2, match.Spans.Count);
        Assert.Equal(new Range(0, 1), match.Spans[0]);
        Assert.Equal(new Range(6, 7), match.Spans[1]);
    }

    [Fact]
    public void Adjacent_matches_collapse_into_one_span()
    {
        Assert.True(FuzzyMatcher.TryMatch("ser", "build-server", out var match));
        Assert.Equal([new Range(6, 9)], match.Spans);
    }
}
