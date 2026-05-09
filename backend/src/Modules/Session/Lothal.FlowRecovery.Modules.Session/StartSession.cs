namespace Lothal.FlowRecovery.Modules.Session;

public sealed record StartSessionCommand(string FlowId, string StartedBy);

public sealed record StartSessionResult(
    bool Success,
    Guid? SessionId,
    string FlowId,
    string Status,
    DateTime? StartedAtUtc,
    string? Error,
    StartSessionOutcome? Outcome,
    SessionNotification? Notification);

public enum StartSessionOutcome
{
    Started,
    DuplicateActiveSession
}

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
            return new StartSessionResult(false, null, string.Empty, "Rejected", null, "FlowId is required.", null, null);
        }

        if (string.IsNullOrWhiteSpace(startedBy))
        {
            return new StartSessionResult(false, null, flowId, "Rejected", null, "StartedBy is required.", null, null);
        }

        var startedAtUtc = DateTime.UtcNow;
        var sessionId = Guid.NewGuid();

        if (!_store.TrySaveIfNoActiveSession(sessionId, flowId, startedBy, startedAtUtc, startedBy, out var session, out var activeSession, out var startedEvent))
        {
            return new StartSessionResult(
                false,
                activeSession!.SessionId,
                activeSession.FlowId,
                activeSession.Status,
                activeSession.StartedAtUtc,
                "Active session already exists.",
                StartSessionOutcome.DuplicateActiveSession,
                null);
        }

        var notification = SessionNotificationMapper.Map(startedEvent!);
        if (notification is null)
        {
            throw new InvalidOperationException("Invariant violation: started outcome must produce a start-session notification.");
        }

        return new StartSessionResult(true, session!.SessionId, session.FlowId, session.Status, session.StartedAtUtc, null, StartSessionOutcome.Started, notification);
    }
}
