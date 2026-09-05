namespace Switchboard.Core.Status;

/// <summary>What a strip segment means. Ordered by "how much you should care".</summary>
public enum TerminalState
{
    /// <summary>No window found for this definition. Segment dims; click launches.</summary>
    Closed = 0,

    /// <summary>Prompt is up and nothing has happened for a while.</summary>
    Idle = 1,

    /// <summary>Output is flowing or a busy-pattern title is showing.</summary>
    Working = 2,

    /// <summary>Wants you: bell, a "done"/"waiting" title, or a taskbar flash. Sticky until focused.</summary>
    Attention = 3,
}
