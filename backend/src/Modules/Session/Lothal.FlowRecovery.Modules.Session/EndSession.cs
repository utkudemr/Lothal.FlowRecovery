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
    EndSessionOutcome? Outcome);

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
            return new EndSessionResult(false, command.SessionId, string.Empty, "Rejected", null, "SessionId is required.", null);
        }

        if (!SessionEndMetadata.TryCreate(command.EndedBy, command.ActorType, command.Reason, out var endMetadata, out var error))
        {
            return new EndSessionResult(false, command.SessionId, string.Empty, "Rejected", null, error, null);
        }

        var outcome = _store.TryEndSession(command.SessionId, endMetadata!, out var session);
        if (outcome == EndSessionOutcome.NotFound)
        {
            return new EndSessionResult(false, command.SessionId, string.Empty, "Rejected", null, "Session not found.", outcome);
        }

        if (outcome == EndSessionOutcome.AlreadyEnded)
        {
            return new EndSessionResult(false, session!.SessionId, session.FlowId, session.Status, session.EndedAtUtc, "Session already ended.", outcome);
        }

        return new EndSessionResult(true, session!.SessionId, session.FlowId, session.Status, session.EndedAtUtc, null, outcome);
    }
}
