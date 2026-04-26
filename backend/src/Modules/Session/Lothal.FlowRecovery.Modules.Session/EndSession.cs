namespace Lothal.FlowRecovery.Modules.Session;

public sealed record EndSessionCommand(
    Guid SessionId,
    string EndedBy,
    string ActorType,
    string? Reason);

public sealed record EndSessionResult(
    bool Success,
    Guid SessionId,
    string FlowId,
    string Status,
    DateTime? EndedAtUtc,
    string? Error,
    EndSessionOutcome? Outcome,
    SessionNotification? Notification);

public enum EndSessionOutcome
{
    NotFound,
    Ended,
    AlreadyEnded,
}

internal sealed class EndSessionHandler
{
    private readonly InMemorySessionStore _store;

    public EndSessionHandler(InMemorySessionStore store)
    {
        _store = store;
    }

    public EndSessionResult Handle(EndSessionCommand command)
    {
        if (command.SessionId == Guid.Empty)
        {
            return new EndSessionResult(false, command.SessionId, string.Empty, "Rejected", null, "SessionId is required.", null, null);
        }

        if (!SessionEndMetadata.TryCreate(command.EndedBy, command.ActorType, command.Reason, out var endMetadata, out var error))
        {
            return new EndSessionResult(false, command.SessionId, string.Empty, "Rejected", null, error, null, null);
        }

        var outcome = _store.TryEndSession(command.SessionId, endMetadata!, out var session, out var endedEvent);
        if (outcome == EndSessionOutcome.NotFound)
        {
            return new EndSessionResult(false, command.SessionId, string.Empty, "Rejected", null, "Session not found.", outcome, null);
        }

        if (outcome == EndSessionOutcome.AlreadyEnded)
        {
            return new EndSessionResult(false, session!.SessionId, session.FlowId, session.Status, session.EndedAtUtc, "Session already ended.", outcome, null);
        }

        var notification = endedEvent is not null
            ? SessionNotificationMapper.Map(endedEvent)
            : null;

        if (notification is null)
        {
            throw new InvalidOperationException("Invariant violation: ended outcome must produce an end-session notification.");
        }

        return new EndSessionResult(true, session!.SessionId, session.FlowId, session.Status, session.EndedAtUtc, null, outcome, notification);
    }
}
