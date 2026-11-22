using Switchboard.Core.Status;
using Xunit;

namespace Switchboard.Core.Tests;

public class StatusRuleValidationTests
{
    [Fact]
    public void A_usable_pattern_reports_no_error()
    {
        Assert.Null(StatusRule.ValidatePattern(@"\b(waiting|done)\b"));
    }

    [Fact]
    public void Every_shipped_default_validates()
    {
        Assert.All(StatusRule.Defaults, rule => Assert.Null(StatusRule.ValidatePattern(rule.Pattern)));
    }

    [Fact]
    public void An_unbalanced_group_reports_an_error()
    {
        Assert.NotNull(StatusRule.ValidatePattern("(unclosed"));
    }

    [Fact]
    public void An_empty_pattern_reports_an_error()
    {
        Assert.NotNull(StatusRule.ValidatePattern("   "));
    }

    [Fact]
    public void A_pattern_the_editor_rejects_is_one_the_evaluator_would_have_ignored()
    {
        const string bad = "(unclosed";
        Assert.NotNull(StatusRule.ValidatePattern(bad));

        // The evaluator's contract for a bad regex is "never matches", not "throws".
        var evaluator = new TitleRuleEvaluator();
        var rules = new[] { new StatusRule { State = TerminalState.Attention, Pattern = bad } };
        Assert.Null(evaluator.Evaluate("unclosed", rules, []));
    }
}
