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
                session.CurrentStep,
                session.StartedAtUtc,
                session.EndedAtUtc,
                session.Events.ToArray());
            return true;
        }
    }

    public SetCurrentStepOutcome TrySetCurrentStep(
        Guid sessionId,
        string currentStep,
        string changedBy,
        string actorType,
        string? reason,
        out SessionRecord? session)
    {
        lock (_sync)
        {
            if (!_sessions.TryGetValue(sessionId, out var currentSession))
            {
                session = null;
                return SetCurrentStepOutcome.NotFound;
            }

            session = currentSession;
            if (session.Status != "Active")
            {
                return SetCurrentStepOutcome.NotActive;
            }

            var changed = session.SetCurrentStep(currentStep, changedBy, actorType, reason, DateTime.UtcNow);
            return changed ? SetCurrentStepOutcome.Changed : SetCurrentStepOutcome.Unchanged;
        }
    }

    public EndSessionOutcome TryEndSession(
        Guid sessionId,
        SessionEndMetadata endMetadata,
        out SessionRecord? session)
    {
        lock (_sync)
        {
            SessionRecord? currentSession;
            if (!_sessions.TryGetValue(sessionId, out currentSession))
            {
                session = null;
                return EndSessionOutcome.NotFound;
            }

            session = currentSession;

            if (session.Status == "Ended")
            {
                var auditOccurredAtUtc = DateTime.UtcNow;
                if (session.EndedAtUtc.HasValue && auditOccurredAtUtc < session.EndedAtUtc.Value)
                {
                    auditOccurredAtUtc = session.EndedAtUtc.Value;
                }

                session.RecordAlreadyEndedAudit(endMetadata, auditOccurredAtUtc);
                return EndSessionOutcome.AlreadyEnded;
            }

            if (session.Status != "Active")
            {
                session = null;
                return EndSessionOutcome.NotFound;
            }

            var endedAtUtc = DateTime.UtcNow;
            var ended = session.End(endMetadata, endedAtUtc);
            if (ended &&
                _activeSessionByFlowId.TryGetValue(session.FlowId, out var activeSessionId) &&
                activeSessionId == session.SessionId)
            {
                _activeSessionByFlowId.Remove(session.FlowId);
            }

            return ended ? EndSessionOutcome.Ended : EndSessionOutcome.NotFound;
        }
    }
}
