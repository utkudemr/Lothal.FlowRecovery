namespace Lothal.FlowRecovery.Modules.Session;

public sealed record StartSessionCommand(string FlowId, string StartedBy);

public sealed record StartSessionResult(
    bool Success,
    Guid? SessionId,
    string FlowId,
    string Status,
    DateTime? StartedAtUtc,
    string? Error);

internal sealed class StartSessionHandler
{
    private readonly InMemorySessionStore _store;

    public StartSessionHandler(InMemorySessionStore store)
    {
        _store = store;
    }

    public StartSessionResult Handle(StartSessionCommand command)
    {
        var flowId = command.FlowId?.Trim() ?? string.Empty;
        var startedBy = command.StartedBy?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(flowId))
        {
            return new StartSessionResult(false, null, string.Empty, "Rejected", null, "FlowId is required.");
        }

        if (string.IsNullOrWhiteSpace(startedBy))
        {
            return new StartSessionResult(false, null, flowId, "Rejected", null, "StartedBy is required.");
        }

        var startedAtUtc = DateTime.UtcNow;
        var startedEvent = new SessionStartedEvent(flowId, startedBy, startedAtUtc);
        var session = SessionRecord.Create(flowId, startedBy, startedAtUtc, startedEvent);

        if (!_store.TrySaveIfNoActiveSession(session))
        {
            return new StartSessionResult(false, null, flowId, "Rejected", null, "Active session already exists.");
        }

        return new StartSessionResult(true, session.SessionId, session.FlowId, session.Status, session.StartedAtUtc, null);
    }
}
