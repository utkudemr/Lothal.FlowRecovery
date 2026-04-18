namespace Lothal.FlowRecovery.Modules.Session;

internal sealed class InMemorySessionStore
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, SessionRecord> _sessions = new();
    private readonly Dictionary<string, Guid> _activeSessionByFlowId = new(StringComparer.OrdinalIgnoreCase);

    public bool TrySaveIfNoActiveSession(SessionRecord session)
    {
        lock (_sync)
        {
            if (_activeSessionByFlowId.ContainsKey(session.FlowId))
            {
                return false;
            }

            _sessions[session.SessionId] = session;
            _activeSessionByFlowId[session.FlowId] = session.SessionId;
            return true;
        }
    }

    public bool TryGetSnapshot(Guid sessionId, out SessionSnapshot? snapshot)
    {
        lock (_sync)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                snapshot = null;
                return false;
            }

            snapshot = new SessionSnapshot(
                session.SessionId,
                session.FlowId,
                session.StartedBy,
                session.Status,
                session.StartedAtUtc,
                session.Events.ToArray());
            return true;
        }
    }
}
