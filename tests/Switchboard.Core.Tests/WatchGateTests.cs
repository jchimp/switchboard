using Switchboard.Core.Registry;
using Xunit;

namespace Switchboard.Core.Tests;

public class WatchGateTests
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(400);
    private static readonly DateTimeOffset T0 = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Burst_of_changes_fires_once_after_quiet_period()
    {
        var gate = new WatchGate(Quiet);
        Assert.NotNull(gate.Poke(T0));
        Assert.NotNull(gate.Poke(T0 + TimeSpan.FromMilliseconds(200)));

        Assert.False(gate.TryFire(T0 + Quiet)); // first poke's timer: superseded
        Assert.True(gate.TryFire(T0 + TimeSpan.FromMilliseconds(600)));
        Assert.False(gate.TryFire(T0 + TimeSpan.FromMilliseconds(700))); // fires only once
    }

    [Fact]
    public void Change_during_mute_window_is_dropped()
    {
        var gate = new WatchGate(Quiet);
        gate.MuteFor(T0, TimeSpan.FromSeconds(2));

        Assert.Null(gate.Poke(T0 + TimeSpan.FromSeconds(1)));
        Assert.False(gate.TryFire(T0 + TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Change_after_mute_expires_fires()
    {
        var gate = new WatchGate(Quiet);
        gate.MuteFor(T0, TimeSpan.FromSeconds(2));

        Assert.NotNull(gate.Poke(T0 + TimeSpan.FromSeconds(3)));
        Assert.True(gate.TryFire(T0 + TimeSpan.FromSeconds(3) + Quiet));
    }

    [Fact]
    public void Second_mute_extends_and_a_shorter_one_never_shortens()
    {
        var gate = new WatchGate(Quiet);
        gate.MuteFor(T0, TimeSpan.FromSeconds(2));
        gate.MuteFor(T0 + TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)); // until T0+3
        gate.MuteFor(T0 + TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(1)); // no-op

        Assert.Null(gate.Poke(T0 + TimeSpan.FromMilliseconds(2900)));
        Assert.NotNull(gate.Poke(T0 + TimeSpan.FromSeconds(3)));
    }
}
