namespace Lothal.FlowRecovery.Modules.Session;

internal sealed class InMemorySessionStore
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, SessionRecord> _sessions = new();
    private readonly Dictionary<string, Guid> _activeSessionByFlowId = new(StringComparer.OrdinalIgnoreCase);

    private static SessionSnapshot CreateSnapshot(SessionRecord session)
    {
        var lastEvent = session.Events[^1];

        return new SessionSnapshot(
            session.SessionId,
            session.FlowId,
            session.StartedBy,
            session.Status,
            session.CurrentStep,
            session.StartedAtUtc,
            lastEvent.OccurredAtUtc,
            lastEvent.GetType().Name,
            session.EndedAtUtc,
            session.Events.ToArray());
    }

    public bool TrySaveIfNoActiveSession(
        Guid sessionId,
        string flowId,
        string startedBy,
        DateTime startedAtUtc,
        string duplicateRequestedBy,
        out SessionRecord? session,
        out SessionRecord? activeSession,
        out SessionStartedEvent? startedEvent)
    {
        lock (_sync)
        {
            startedEvent = null;

            if (_activeSessionByFlowId.TryGetValue(flowId, out var activeSessionId) &&
                _sessions.TryGetValue(activeSessionId, out activeSession))
            {
                activeSession.RecordDuplicateStartAudit(duplicateRequestedBy, DateTime.UtcNow);
                session = null;
                return false;
            }

            startedEvent = new SessionStartedEvent(sessionId, flowId, startedBy, startedAtUtc);
            session = SessionRecord.Create(sessionId, flowId, startedBy, startedAtUtc, startedEvent);
            _sessions[session.SessionId] = session;
            _activeSessionByFlowId[session.FlowId] = session.SessionId;
            activeSession = null;
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

            snapshot = CreateSnapshot(session);
            return true;
        }
    }

    public bool TryGetActiveSnapshotByFlowId(string flowId, out SessionSnapshot? snapshot)
    {
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(flowId))
            {
                snapshot = null;
                return false;
            }

            var normalizedFlowId = flowId.Trim();
            if (!_activeSessionByFlowId.TryGetValue(normalizedFlowId, out var sessionId) ||
                !_sessions.TryGetValue(sessionId, out var session) ||
                session.Status != "Active")
            {
                snapshot = null;
                return false;
            }

            snapshot = CreateSnapshot(session);
            return true;
        }
    }

    public IReadOnlyList<SessionSnapshot> GetActiveSessions()
    {
        lock (_sync)
        {
            return _activeSessionByFlowId.Values
                .Select(sessionId => _sessions[sessionId])
                .Where(session => session.Status == "Active")
                .OrderBy(session => session.StartedAtUtc)
                .ThenBy(session => session.SessionId)
                .Select(CreateSnapshot)
                .ToArray();
        }
    }

    public IReadOnlyList<SessionSnapshot> GetStaleActiveSessions(DateTime staleBeforeUtc)
    {
        lock (_sync)
        {
            return _activeSessionByFlowId.Values
                .Select(sessionId => _sessions[sessionId])
                .Where(session => session.Status == "Active" && session.Events[^1].OccurredAtUtc <= staleBeforeUtc)
                .OrderBy(session => session.Events[^1].OccurredAtUtc)
                .ThenBy(session => session.SessionId)
                .Select(CreateSnapshot)
                .ToArray();
        }
    }

    public SetCurrentStepOutcome TrySetCurrentStep(
        Guid sessionId,
        ISessionCurrentStepValidator currentStepValidator,
        string currentStep,
        SessionCurrentStepMetadata metadata,
        out SessionRecord? session,
        out SessionCurrentStepSetEvent? stepSetEvent,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(currentStepValidator);
        ArgumentNullException.ThrowIfNull(metadata);

        lock (_sync)
        {
            if (!_sessions.TryGetValue(sessionId, out var currentSession))
            {
                session = null;
                stepSetEvent = null;
                error = "Session not found.";
                return SetCurrentStepOutcome.NotFound;
            }

            session = currentSession;
            if (session.Status != "Active")
            {
                stepSetEvent = null;
                error = "Session is not active.";
                var rejectedOccurredAtUtc = DateTime.UtcNow;
                if (session.EndedAtUtc.HasValue && rejectedOccurredAtUtc < session.EndedAtUtc.Value)
                {
                    rejectedOccurredAtUtc = session.EndedAtUtc.Value;
                }

                session.RecordCurrentStepRejectedNotActive(
                    currentStep,
                    metadata,
                    rejectedOccurredAtUtc);
                return SetCurrentStepOutcome.NotActive;
            }

            var stepValidation = currentStepValidator.Validate(session.FlowId, session.CurrentStep, currentStep);
            return session.SetCurrentStep(stepValidation, currentStep, metadata, DateTime.UtcNow, out stepSetEvent, out error);
        }
    }

    public EndSessionOutcome TryEndSession(
        Guid sessionId,
        SessionEndMetadata endMetadata,
        out SessionRecord? session,
        out SessionEndedEvent? endedEvent)
    {
        lock (_sync)
        {
            endedEvent = null;
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
            if (ended)
            {
                endedEvent = session.Events[^1] as SessionEndedEvent;
            }

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
