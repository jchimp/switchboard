using Switchboard.Core.Status;
using Xunit;

namespace Switchboard.Core.Tests;

public class MotionSamplerTests
{
    [Fact]
    public void Batch_is_capped_at_three()
    {
        var sampler = new MotionSampler();
        var batch = sampler.NextBatch(["a", "b", "c", "d", "e"]);
        Assert.Equal(3, batch.Count);
    }

    [Fact]
    public void Round_robin_covers_every_window_across_ticks()
    {
        var sampler = new MotionSampler();
        var eligible = new[] { "a", "b", "c", "d", "e" };
        var seen = new HashSet<string>();
        seen.UnionWith(sampler.NextBatch(eligible));
        seen.UnionWith(sampler.NextBatch(eligible));
        Assert.Equal(eligible.ToHashSet(), seen);
    }

    [Fact]
    public void Small_eligible_set_is_sampled_every_tick()
    {
        var sampler = new MotionSampler();
        Assert.Equal(["a", "b"], sampler.NextBatch(["a", "b"]));
        Assert.Equal(["a", "b"], sampler.NextBatch(["a", "b"]));
    }

    [Fact]
    public void Empty_eligible_set_yields_nothing()
    {
        Assert.Empty(new MotionSampler().NextBatch([]));
    }

    [Fact]
    public void Shrinking_eligible_set_does_not_throw()
    {
        var sampler = new MotionSampler();
        sampler.NextBatch(["a", "b", "c", "d", "e"]); // cursor advances past the next list's end
        Assert.Single(sampler.NextBatch(["a"]));
    }

    [Fact]
    public void First_observation_is_a_baseline_not_motion()
    {
        var sampler = new MotionSampler();
        Assert.False(sampler.Observe("a", 123));
    }

    [Fact]
    public void Changed_hash_is_motion_and_unchanged_is_not()
    {
        var sampler = new MotionSampler();
        sampler.Observe("a", 123);
        Assert.True(sampler.Observe("a", 456));
        Assert.False(sampler.Observe("a", 456));
    }

    [Fact]
    public void Forgetting_a_window_resets_its_baseline()
    {
        var sampler = new MotionSampler();
        sampler.Observe("a", 123);
        sampler.Forget("a");
        Assert.False(sampler.Observe("a", 456));
    }

    [Theory]
    [InlineData(TerminalState.Idle, false, true)]
    [InlineData(TerminalState.Working, false, true)]
    [InlineData(TerminalState.Idle, true, false)]
    [InlineData(TerminalState.Attention, false, false)]
    [InlineData(TerminalState.Closed, false, false)]
    public void Only_unfocused_idle_or_working_windows_are_eligible(TerminalState state, bool focused, bool expected)
    {
        Assert.Equal(expected, MotionSampler.IsEligible(state, focused));
    }

    [Fact]
    public void Fnv1a_distinguishes_inputs_and_is_stable()
    {
        var a = MotionSampler.Fnv1a([1, 2, 3]);
        Assert.Equal(a, MotionSampler.Fnv1a([1, 2, 3]));
        Assert.NotEqual(a, MotionSampler.Fnv1a([1, 2, 4]));
    }
}
