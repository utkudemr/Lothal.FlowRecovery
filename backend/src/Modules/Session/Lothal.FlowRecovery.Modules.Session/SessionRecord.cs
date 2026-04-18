namespace Lothal.FlowRecovery.Modules.Session;

public sealed class SessionRecord
{
    private readonly List<SessionEvent> _events;

    public Guid SessionId { get; }
    public string FlowId { get; }
    public string StartedBy { get; }
    public string Status { get; private set; }
    public DateTime StartedAtUtc { get; }
    public IReadOnlyList<SessionEvent> Events => _events;

    private SessionRecord(
        Guid sessionId,
        string flowId,
        string startedBy,
        string status,
        DateTime startedAtUtc,
        List<SessionEvent> events)
    {
        SessionId = sessionId;
        FlowId = flowId;
        StartedBy = startedBy;
        Status = status;
        StartedAtUtc = startedAtUtc;
        _events = events;
    }

    public static SessionRecord Create(
        string flowId,
        string startedBy,
        DateTime startedAtUtc,
        SessionStartedEvent startedEvent)
    {
        return new SessionRecord(
            Guid.NewGuid(),
            flowId,
            startedBy,
            "Active",
            startedAtUtc,
            new List<SessionEvent> { startedEvent });
    }
}

public abstract record SessionEvent(DateTime OccurredAtUtc);

public sealed record SessionStartedEvent(
    string FlowId,
    string StartedBy,
    DateTime OccurredAtUtc) : SessionEvent(OccurredAtUtc);
