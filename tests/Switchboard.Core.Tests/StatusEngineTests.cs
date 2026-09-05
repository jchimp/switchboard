using Microsoft.Extensions.Time.Testing;
using Switchboard.Core.Registry;
using Switchboard.Core.Status;
using Xunit;

namespace Switchboard.Core.Tests;

public class StatusEngineTests
{
    private static readonly TerminalDefinition Anvil = new()
    {
        Name = "anvil",
        Color = "#B07CF5",
        Profile = "Ubuntu",
        StatusRules = [new StatusRule { State = TerminalState.Attention, Pattern = @"\bwaiting\b" }],
    };

    private static (StatusEngine Engine, FakeTimeProvider Time, List<(string, TerminalState)> Events) Make()
    {
        var time = new FakeTimeProvider();
        var engine = new StatusEngine(time, StatusRule.Defaults, idleAfter: TimeSpan.FromSeconds(20));
        var events = new List<(string, TerminalState)>();
        engine.StateChanged += (n, s) => events.Add((n, s));
        return (engine, time, events);
    }

    [Fact]
    public void Appearing_window_starts_idle()
    {
        var (engine, _, events) = Make();
        engine.WindowAppeared(Anvil);
        Assert.Equal(TerminalState.Idle, engine.StateOf("anvil"));
        Assert.Single(events);
    }

    [Fact]
    public void Terminal_rules_win_over_global_rules()
    {
        var (engine, _, _) = Make();
        engine.WindowAppeared(Anvil);
        engine.TitleChanged(Anvil, "claude — waiting for input (running)");
        Assert.Equal(TerminalState.Attention, engine.StateOf("anvil"));
    }

    [Fact]
    public void Observed_output_marks_working_and_decays_to_idle()
    {
        var (engine, time, _) = Make();
        engine.WindowAppeared(Anvil);
        engine.OutputObserved(Anvil);
        Assert.Equal(TerminalState.Working, engine.StateOf("anvil"));

        time.Advance(TimeSpan.FromSeconds(21));
        engine.Tick();
        Assert.Equal(TerminalState.Idle, engine.StateOf("anvil"));
    }

    [Fact]
    public void Observed_output_never_downgrades_attention()
    {
        var (engine, _, _) = Make();
        engine.WindowAppeared(Anvil);
        engine.Flashed(Anvil);
        engine.OutputObserved(Anvil);
        Assert.Equal(TerminalState.Attention, engine.StateOf("anvil"));
    }

    [Fact]
    public void Attention_is_sticky_until_focused()
    {
        var (engine, time, _) = Make();
        engine.WindowAppeared(Anvil);
        engine.Flashed(Anvil);
        engine.TitleChanged(Anvil, "building things");
        time.Advance(TimeSpan.FromMinutes(5));
        engine.Tick();
        Assert.Equal(TerminalState.Attention, engine.StateOf("anvil"));

        engine.Focused("anvil");
        Assert.Equal(TerminalState.Idle, engine.StateOf("anvil"));
    }

    [Fact]
    public void Title_driven_attention_is_sticky_until_focused()
    {
        var (engine, time, _) = Make();
        engine.WindowAppeared(Anvil);
        engine.TitleChanged(Anvil, "claude — waiting for input");
        Assert.Equal(TerminalState.Attention, engine.StateOf("anvil"));

        // Later activity — matched or not — and idle decay must not demote it.
        engine.TitleChanged(Anvil, "npm run build — building");
        engine.TitleChanged(Anvil, "~/anvil");
        time.Advance(TimeSpan.FromMinutes(5));
        engine.Tick();
        Assert.Equal(TerminalState.Attention, engine.StateOf("anvil"));

        engine.Focused("anvil");
        Assert.Equal(TerminalState.Idle, engine.StateOf("anvil"));
    }

    [Fact]
    public void Working_decays_to_idle_after_timeout()
    {
        var (engine, time, _) = Make();
        engine.WindowAppeared(Anvil);
        engine.TitleChanged(Anvil, "npm run build — building");
        Assert.Equal(TerminalState.Working, engine.StateOf("anvil"));

        time.Advance(TimeSpan.FromSeconds(19));
        engine.Tick();
        Assert.Equal(TerminalState.Working, engine.StateOf("anvil"));

        time.Advance(TimeSpan.FromSeconds(2));
        engine.Tick();
        Assert.Equal(TerminalState.Idle, engine.StateOf("anvil"));
    }

    [Fact]
    public void Unmatched_title_change_counts_as_activity()
    {
        var (engine, _, _) = Make();
        engine.WindowAppeared(Anvil);
        engine.TitleChanged(Anvil, "~/anvil");
        Assert.Equal(TerminalState.Working, engine.StateOf("anvil"));
    }

    [Fact]
    public void Window_gone_reports_closed_once()
    {
        var (engine, _, events) = Make();
        engine.WindowAppeared(Anvil);
        engine.WindowGone("anvil");
        engine.WindowGone("anvil");
        Assert.Equal(TerminalState.Closed, engine.StateOf("anvil"));
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void Bad_regex_never_matches_and_never_throws()
    {
        var evaluator = new TitleRuleEvaluator();
        var rules = new[] { new StatusRule { State = TerminalState.Attention, Pattern = "(" } };
        Assert.Null(evaluator.Evaluate("anything", rules, []));
    }
}
