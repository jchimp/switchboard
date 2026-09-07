using Switchboard.Core.Focus;
using Xunit;

namespace Switchboard.Core.Tests;

public class MruListTests
{
    [Fact]
    public void Touch_moves_an_existing_name_to_the_front()
    {
        var mru = new MruList();
        mru.Touch("a");
        mru.Touch("b");
        mru.Touch("a");
        Assert.Equal(["a", "b"], mru.Order);
    }

    [Fact]
    public void Touching_an_unknown_name_inserts_it_at_the_front()
    {
        var mru = new MruList();
        mru.Touch("a");
        mru.Touch("b");
        Assert.Equal(["b", "a"], mru.Order);
    }

    [Fact]
    public void Rename_keeps_the_entrys_position()
    {
        var mru = new MruList();
        mru.Touch("a");
        mru.Touch("b");
        mru.Touch("c");
        mru.Rename("b", "b2");
        Assert.Equal(["c", "b2", "a"], mru.Order);
    }

    [Fact]
    public void Renaming_an_unknown_name_changes_nothing()
    {
        var mru = new MruList();
        mru.Touch("a");
        mru.Rename("x", "y");
        Assert.Equal(["a"], mru.Order);
    }

    [Fact]
    public void Forget_removes_without_reordering_others()
    {
        var mru = new MruList();
        mru.Touch("a");
        mru.Touch("b");
        mru.Touch("c");
        mru.Forget("b");
        Assert.Equal(["c", "a"], mru.Order);
    }

    [Fact]
    public void Prune_drops_names_absent_from_the_registry()
    {
        var mru = new MruList();
        mru.Touch("a");
        mru.Touch("b");
        mru.Touch("c");
        mru.Prune(new HashSet<string> { "a", "c" });
        Assert.Equal(["c", "a"], mru.Order);
    }
}
