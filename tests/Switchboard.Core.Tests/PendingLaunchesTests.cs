using Microsoft.Extensions.Time.Testing;
using Switchboard.Core.Launch;
using Xunit;

namespace Switchboard.Core.Tests;

public sealed class PendingLaunchesTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void A_started_launch_is_in_flight_until_it_completes()
    {
        var pending = new PendingLaunches(_time, Timeout);
        pending.Start("term-1");
        Assert.True(pending.IsInFlight("term-1"));

        pending.Complete("term-1");
        Assert.False(pending.IsInFlight("term-1"));
    }

    [Fact]
    public void A_launch_is_no_longer_in_flight_after_the_timeout()
    {
        var pending = new PendingLaunches(_time, Timeout);
        pending.Start("term-1");

        _time.Advance(Timeout + TimeSpan.FromMilliseconds(1));
        Assert.False(pending.IsInFlight("term-1"));
    }

    [Fact]
    public void Cancel_allows_an_immediate_relaunch()
    {
        var pending = new PendingLaunches(_time, Timeout);
        pending.Start("term-1");

        pending.Cancel("term-1");
        Assert.False(pending.IsInFlight("term-1"));
    }

    [Fact]
    public void Complete_reports_whether_a_launch_was_pending()
    {
        var pending = new PendingLaunches(_time, Timeout);
        pending.Start("term-1");

        Assert.True(pending.Complete("term-1"));
        Assert.False(pending.Complete("term-1")); // a recovery or adopt bind is not a launch
        Assert.False(pending.Complete("term-2"));
    }

    [Fact]
    public void Complete_after_the_timeout_does_not_ask_for_focus()
    {
        var pending = new PendingLaunches(_time, Timeout);
        pending.Start("term-1");

        _time.Advance(Timeout + TimeSpan.FromMilliseconds(1));
        Assert.False(pending.Complete("term-1"));
    }

    [Fact]
    public void Launches_are_tracked_per_name()
    {
        var pending = new PendingLaunches(_time, Timeout);
        pending.Start("term-1");

        Assert.False(pending.IsInFlight("term-2"));
    }
}
