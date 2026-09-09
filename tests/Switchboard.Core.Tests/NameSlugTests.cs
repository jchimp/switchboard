using System.Text.RegularExpressions;
using Switchboard.Core.Registry;
using Xunit;

namespace Switchboard.Core.Tests;

public class NameSlugTests
{
    private static readonly HashSet<string> Nothing = new(StringComparer.Ordinal);

    [Fact]
    public void A_plain_title_becomes_itself()
    {
        Assert.Equal("anvil", NameSlug.FromTitle("anvil", "term-", Nothing));
    }

    [Fact]
    public void Path_noise_after_the_name_collapses_to_hyphens()
    {
        Assert.Equal("anvil-anvil", NameSlug.FromTitle("anvil — ~/anvil", "term-", Nothing));
    }

    [Fact]
    public void Case_and_spaces_are_normalised()
    {
        Assert.Equal("windows-powershell", NameSlug.FromTitle("Windows PowerShell", "term-", Nothing));
    }

    [Fact]
    public void Leading_and_trailing_punctuation_is_trimmed()
    {
        Assert.Equal("ubuntu", NameSlug.FromTitle("  ***Ubuntu***  ", "term-", Nothing));
    }

    [Fact]
    public void A_title_with_nothing_usable_falls_back_to_the_prefix()
    {
        Assert.Equal("term", NameSlug.FromTitle("—— ///", "term-", Nothing));
    }

    [Fact]
    public void An_empty_title_falls_back_to_the_prefix()
    {
        Assert.Equal("term", NameSlug.FromTitle(null, "term-", Nothing));
    }

    [Fact]
    public void A_taken_name_gains_a_number()
    {
        var taken = new HashSet<string>(StringComparer.Ordinal) { "anvil" };
        Assert.Equal("anvil-2", NameSlug.FromTitle("anvil", "term-", taken));
    }

    [Fact]
    public void Numbering_keeps_going_until_it_finds_a_free_name()
    {
        var taken = new HashSet<string>(StringComparer.Ordinal) { "anvil", "anvil-2", "anvil-3" };
        Assert.Equal("anvil-4", NameSlug.FromTitle("anvil", "term-", taken));
    }

    [Fact]
    public void Long_titles_are_truncated_to_the_name_limit()
    {
        var slug = NameSlug.FromTitle(new string('a', 80), "term-", Nothing);
        Assert.Equal(NameSlug.MaxLength, slug.Length);
    }

    [Fact]
    public void A_truncated_name_still_has_room_for_its_number()
    {
        var taken = new HashSet<string>(StringComparer.Ordinal) { new('a', NameSlug.MaxLength) };
        var slug = NameSlug.FromTitle(new string('a', 80), "term-", taken);
        Assert.True(slug.Length <= NameSlug.MaxLength);
        Assert.EndsWith("-2", slug);
    }

    [Fact]
    public void Every_proposed_name_passes_the_definition_name_rule()
    {
        string[] titles =
        [
            "anvil — ~/anvil", "Windows PowerShell", "  ***Ubuntu***  ", "—— ///", null!,
            new('a', 80), "2fast", "-leading-hyphen-", "MiXeD CaSe 123",
        ];

        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var title in titles)
        {
            var name = NameSlug.FromTitle(title, "term-", taken);
            Assert.Matches(new Regex("^[a-z0-9][a-z0-9-]{0,31}$"), name);
            Assert.True(taken.Add(name), $"'{name}' was proposed twice");
        }
    }
}
