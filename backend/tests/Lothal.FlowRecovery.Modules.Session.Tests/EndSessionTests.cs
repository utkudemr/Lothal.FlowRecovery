using Lothal.FlowRecovery.Modules.Session;

namespace Lothal.FlowRecovery.Modules.Session.Tests;

public sealed class EndSessionTests
{
    [Fact]
    public void EndSession_ShouldRejectInvalidCommands()
    {
        var module = new SessionModule();
        var sessionId = Guid.NewGuid();

        var missingSessionId = module.EndSession(new EndSessionCommand(Guid.Empty, "operator-a", "operator", "done"));
        var missingEndedBy = module.EndSession(new EndSessionCommand(sessionId, " ", "operator", "done"));
        var missingActorType = module.EndSession(new EndSessionCommand(sessionId, "operator-a", " ", "done"));
        var missingOperatorReason = module.EndSession(new EndSessionCommand(sessionId, "operator-a", "oPeRaToR", " "));
        var unknownActorType = module.EndSession(new EndSessionCommand(sessionId, "operator-a", "auditor", "done"));

        Assert.False(missingSessionId.Success);
        Assert.Equal("SessionId is required.", missingSessionId.Error);
        Assert.False(missingEndedBy.Success);
        Assert.Equal("EndedBy is required.", missingEndedBy.Error);
        Assert.False(missingActorType.Success);
        Assert.Equal("ActorType is required.", missingActorType.Error);
        Assert.False(missingOperatorReason.Success);
        Assert.Equal("Reason is required for operator end.", missingOperatorReason.Error);
        Assert.False(unknownActorType.Success);
        Assert.Equal("ActorType is invalid.", unknownActorType.Error);
    }

    [Fact]
    public void SessionEndMetadata_ShouldNormalizeAndValidateValues()
    {
        Assert.True(SessionEndMetadata.TryCreate(" operator-a ", "oPeRaToR", "  completed  ", out var operatorMetadata, out var operatorError));
        Assert.NotNull(operatorMetadata);
        Assert.Null(operatorError);
        Assert.Equal("operator-a", operatorMetadata!.EndedBy);
        Assert.Equal("Operator", operatorMetadata.ActorType);
        Assert.Equal("completed", operatorMetadata.Reason);

        Assert.True(SessionEndMetadata.TryCreate("system-a", "system", "   ", out var systemMetadata, out var systemError));
        Assert.NotNull(systemMetadata);
        Assert.Null(systemError);
        Assert.Equal("system-a", systemMetadata!.EndedBy);
        Assert.Equal("System", systemMetadata.ActorType);
        Assert.Null(systemMetadata.Reason);

        Assert.False(SessionEndMetadata.TryCreate(" ", "Operator", "done", out _, out var missingEndedByError));
        Assert.Equal("EndedBy is required.", missingEndedByError);
        Assert.False(SessionEndMetadata.TryCreate("operator-a", "auditor", "done", out _, out var invalidActorError));
        Assert.Equal("ActorType is invalid.", invalidActorError);
        Assert.False(SessionEndMetadata.TryCreate("operator-a", "Operator", " ", out _, out var missingReasonError));
        Assert.Equal("Reason is required for operator end.", missingReasonError);
    }

    [Fact]
    public void EndSession_ShouldReject_WhenSessionDoesNotExist()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var result = module.EndSession(new EndSessionCommand(Guid.NewGuid(), "operator-a", "Operator", "done"));
        var existing = module.GetSession(start.SessionId!.Value);

        Assert.False(result.Success);
        Assert.Equal("Session not found.", result.Error);
        Assert.Equal(EndSessionOutcome.NotFound, result.Outcome);
        Assert.NotNull(existing);
        Assert.Equal("Active", existing.Status);
        Assert.Single(existing.Events);
    }

    [Fact]
    public void EndSession_ShouldEndActiveSession_AndAppendSessionEndedEvent()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var result = module.EndSession(new EndSessionCommand(start.SessionId!.Value, "operator-b", "oPeRaToR", "completed"));
        var session = module.GetSession(start.SessionId.Value);

        Assert.True(result.Success);
        Assert.Equal(start.SessionId.Value, result.SessionId);
        Assert.Equal(flowId, result.FlowId);
        Assert.Equal("Ended", result.Status);
        Assert.NotNull(result.EndedAtUtc);
        Assert.Null(result.Error);
        Assert.Equal(EndSessionOutcome.Ended, result.Outcome);

        Assert.NotNull(session);
        Assert.Equal("Ended", session.Status);
        Assert.Equal(2, session.Events.Count);

        var endedEvent = Assert.IsType<SessionEndedEvent>(session.Events[1]);
        Assert.Equal(start.SessionId.Value, endedEvent.SessionId);
        Assert.Equal(flowId, endedEvent.FlowId);
        Assert.Equal("operator-b", endedEvent.EndedBy);
        Assert.Equal("Operator", endedEvent.ActorType);
        Assert.Equal("completed", endedEvent.Reason);
        Assert.Equal("Active", endedEvent.PreviousStatus);
        Assert.Equal("Ended", endedEvent.NewStatus);
        Assert.Equal(result.EndedAtUtc, endedEvent.OccurredAtUtc);
    }

    [Fact]
    public void EndSession_ShouldNormalizeWhitespaceReasonToNull()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var result = module.EndSession(new EndSessionCommand(start.SessionId!.Value, "system", "System", "   "));
        var session = module.GetSession(start.SessionId.Value);

        Assert.True(result.Success);
        Assert.NotNull(session);
        var endedEvent = Assert.IsType<SessionEndedEvent>(session.Events[1]);
        Assert.Null(endedEvent.Reason);
    }

    [Fact]
    public void EndSession_ShouldReturnAlreadyEnded_WhenSessionAlreadyEnded()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var first = module.EndSession(new EndSessionCommand(start.SessionId!.Value, "operator-b", "Operator", "completed"));
        var beforeRetryAttemptUtc = DateTime.UtcNow;
        var second = module.EndSession(new EndSessionCommand(start.SessionId.Value, "operator-b", "Operator", "completed"));
        var afterRetryAttemptUtc = DateTime.UtcNow;
        var session = module.GetSession(start.SessionId.Value);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal(EndSessionOutcome.AlreadyEnded, second.Outcome);
        Assert.Equal("Ended", second.Status);
        Assert.Equal(first.EndedAtUtc, second.EndedAtUtc);

        Assert.NotNull(session);
        Assert.Equal("Ended", session.Status);
        Assert.Equal(3, session.Events.Count);
        Assert.Single(session.Events.OfType<SessionEndedEvent>());
        Assert.Single(session.Events.OfType<SessionEndAlreadyEndedAuditEvent>());

        var endedEvent = Assert.IsType<SessionEndedEvent>(session.Events[1]);
        Assert.Equal("operator-b", endedEvent.EndedBy);
        Assert.Equal("completed", endedEvent.Reason);

        var auditEvent = Assert.IsType<SessionEndAlreadyEndedAuditEvent>(session.Events[2]);
        Assert.Equal(start.SessionId.Value, auditEvent.SessionId);
        Assert.Equal(flowId, auditEvent.FlowId);
        Assert.Equal("operator-b", auditEvent.EndedBy);
        Assert.Equal("Operator", auditEvent.ActorType);
        Assert.Equal("completed", auditEvent.Reason);
        Assert.Equal("Ended", auditEvent.CurrentStatus);
        Assert.Equal(first.EndedAtUtc, auditEvent.ExistingEndedAtUtc);
        Assert.InRange(auditEvent.OccurredAtUtc, beforeRetryAttemptUtc, afterRetryAttemptUtc);
        Assert.NotNull(first.EndedAtUtc);
        Assert.True(auditEvent.OccurredAtUtc >= first.EndedAtUtc.Value);
    }

    [Fact]
    public void EndSession_ShouldReturnAlreadyEnded_WhenSessionAlreadyEnded_WithDifferentOperatorMetadata()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var first = module.EndSession(new EndSessionCommand(start.SessionId!.Value, "operator-b", "Operator", "completed"));
        var second = module.EndSession(new EndSessionCommand(start.SessionId.Value, "operator-c", "oPeRaToR", "duplicate"));
        var session = module.GetSession(start.SessionId.Value);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal(EndSessionOutcome.AlreadyEnded, second.Outcome);
        Assert.Equal("Ended", second.Status);
        Assert.Equal(first.EndedAtUtc, second.EndedAtUtc);

        Assert.NotNull(session);
        var auditEvent = Assert.IsType<SessionEndAlreadyEndedAuditEvent>(session.Events[2]);
        Assert.Equal("operator-c", auditEvent.EndedBy);
        Assert.Equal("Operator", auditEvent.ActorType);
        Assert.Equal("duplicate", auditEvent.Reason);
    }

    [Fact]
    public async Task EndSession_ShouldPreserveAuditChronology_WhenDuplicateEndRequestsRace()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));
        var sessionId = start.SessionId!.Value;
        const int attemptCount = 24;
        using var ready = new CountdownEvent(attemptCount);
        using var startGate = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, attemptCount)
            .Select(index => Task.Factory.StartNew(() =>
            {
                ready.Signal();
                startGate.Wait();

                return module.EndSession(new EndSessionCommand(sessionId, $"operator-{index}", "Operator", "duplicate"));
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))
            .ToArray();

        Assert.True(ready.Wait(TimeSpan.FromSeconds(5)));
        startGate.Set();
        var results = await Task.WhenAll(tasks);
        var session = module.GetSession(sessionId);

        Assert.NotNull(session);
        Assert.Equal("Ended", session.Status);
        Assert.Equal(attemptCount + 1, session.Events.Count);
        Assert.Single(results, result => result.Outcome == EndSessionOutcome.Ended);
        Assert.Equal(attemptCount - 1, results.Count(result => result.Outcome == EndSessionOutcome.AlreadyEnded));

        var endedEvent = Assert.Single(session.Events.OfType<SessionEndedEvent>());
        var auditEvents = session.Events.OfType<SessionEndAlreadyEndedAuditEvent>().ToArray();
        Assert.Equal(attemptCount - 1, auditEvents.Length);
        Assert.NotNull(session.EndedAtUtc);
        Assert.Equal(session.EndedAtUtc, endedEvent.OccurredAtUtc);
        Assert.All(auditEvents, auditEvent =>
        {
            Assert.Equal(session.EndedAtUtc, auditEvent.ExistingEndedAtUtc);
            Assert.True(auditEvent.OccurredAtUtc >= session.EndedAtUtc.Value);
        });
    }

    [Fact]
    public void EndSession_ShouldReleaseActiveFlowIndex_AllowingNewStart()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var firstStart = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var end = module.EndSession(new EndSessionCommand(firstStart.SessionId!.Value, "operator-a", "Operator", "done"));
        var secondStart = module.StartSession(new StartSessionCommand(flowId, "operator-b"));

        Assert.True(end.Success);
        Assert.True(secondStart.Success);
        Assert.NotEqual(firstStart.SessionId, secondStart.SessionId);
        Assert.Equal("Active", secondStart.Status);
    }

    [Fact]
    public void GetSession_ShouldReturnEndedSessionSnapshot_AfterEndSession()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var result = module.EndSession(new EndSessionCommand(start.SessionId!.Value, "system", "System", null));

        var session = module.GetSession(start.SessionId.Value);

        Assert.NotNull(session);
        Assert.Equal(start.SessionId.Value, session.SessionId);
        Assert.Equal(flowId, session.FlowId);
        Assert.Equal("operator-a", session.StartedBy);
        Assert.Equal("Ended", session.Status);
        Assert.Equal(start.StartedAtUtc, session.StartedAtUtc);
        Assert.NotNull(session.EndedAtUtc);
        Assert.Equal(result.EndedAtUtc, session.EndedAtUtc);
        Assert.Equal(2, session.Events.Count);
        Assert.IsType<SessionStartedEvent>(session.Events[0]);
        Assert.IsType<SessionEndedEvent>(session.Events[1]);
    }

    [Fact]
    public void SessionRecordEnd_ShouldEnforceActorInvariants()
    {
        var startedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        var sessionId = Guid.NewGuid();
        Assert.True(SessionEndMetadata.TryCreate("operator-a", "Operator", "done", out var validMetadata, out _));
        var session = SessionRecord.Create(
            sessionId,
            "flow-record-invariants",
            "operator-a",
            startedAtUtc,
            new SessionStartedEvent(sessionId, "flow-record-invariants", "operator-a", startedAtUtc));

        Assert.Throws<ArgumentException>(() => session.End(validMetadata!, default));

        Assert.Equal("Active", session.Status);
        Assert.Null(session.EndedAtUtc);
        Assert.Single(session.Events);
    }

    [Fact]
    public void SessionRecordCreate_ShouldReject_WhenStartedEventSessionIdDiffers()
    {
        var startedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        var sessionId = Guid.NewGuid();

        var mismatch = new SessionStartedEvent(
            Guid.NewGuid(),
            "flow-record-mismatch",
            "operator-a",
            startedAtUtc);

        var exception = Assert.Throws<ArgumentException>(() => SessionRecord.Create(
            sessionId,
            "flow-record-mismatch",
            "operator-a",
            startedAtUtc,
            mismatch));

        Assert.Equal("startedEvent", exception.ParamName);
        Assert.Contains("Started event session id must match the session id.", exception.Message);
    }

    [Fact]
    public void SessionRecordCreate_ShouldReject_WhenSessionIdIsEmpty()
    {
        var startedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        var sessionId = Guid.NewGuid();

        var startedEvent = new SessionStartedEvent(
            sessionId,
            "flow-record-empty-session-id",
            "operator-a",
            startedAtUtc);

        var exception = Assert.Throws<ArgumentException>(() => SessionRecord.Create(
            Guid.Empty,
            "flow-record-empty-session-id",
            "operator-a",
            startedAtUtc,
            startedEvent));

        Assert.Equal("sessionId", exception.ParamName);
        Assert.Contains("Session id is required.", exception.Message);
    }

    [Fact]
    public void SessionRecordEnd_ShouldReject_WhenEndedAtUtcPrecedesStartedAtUtc()
    {
        var startedAtUtc = DateTime.UtcNow;
        var endedAtUtc = startedAtUtc.AddTicks(-1);
        var sessionId = Guid.NewGuid();
        Assert.True(SessionEndMetadata.TryCreate("operator-a", "Operator", "done", out var validMetadata, out _));
        var session = SessionRecord.Create(
            sessionId,
            "flow-record-chronology",
            "operator-a",
            startedAtUtc,
            new SessionStartedEvent(sessionId, "flow-record-chronology", "operator-a", startedAtUtc));

        Assert.Throws<ArgumentException>(() => session.End(validMetadata!, endedAtUtc));

        Assert.Equal("Active", session.Status);
        Assert.Null(session.EndedAtUtc);
        Assert.Single(session.Events);
    }

    [Fact]
    public void SessionRecordEnd_ShouldNotChangeStateOrAppendEndedEvent_WhenAlreadyEnded()
    {
        var startedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        var firstEndedAtUtc = DateTime.UtcNow;
        var sessionId = Guid.NewGuid();
        Assert.True(SessionEndMetadata.TryCreate("operator-a", "Operator", "done", out var firstMetadata, out _));
        Assert.True(SessionEndMetadata.TryCreate("operator-b", "Operator", "duplicate", out var secondMetadata, out _));
        var session = SessionRecord.Create(
            sessionId,
            "flow-record-repeat",
            "operator-a",
            startedAtUtc,
            new SessionStartedEvent(sessionId, "flow-record-repeat", "operator-a", startedAtUtc));

        var first = session.End(firstMetadata!, firstEndedAtUtc);
        var second = session.End(secondMetadata!, default);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal("Ended", session.Status);
        Assert.Equal(firstEndedAtUtc, session.EndedAtUtc);
        Assert.Equal(2, session.Events.Count);
        Assert.Single(session.Events.OfType<SessionEndedEvent>());
    }

    [Fact]
    public void SessionRecordRecordAlreadyEndedAudit_ShouldUseUtcNowTimestamp_AndRespectChronology()
    {
        var startedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        var endedAtUtc = DateTime.UtcNow;
        var auditOccurredAtUtc = DateTime.UtcNow;
        var sessionId = Guid.NewGuid();
        Assert.True(SessionEndMetadata.TryCreate("operator-a", "Operator", "done", out var metadata, out _));
        var session = SessionRecord.Create(
            sessionId,
            "flow-record-audit",
            "operator-a",
            startedAtUtc,
            new SessionStartedEvent(sessionId, "flow-record-audit", "operator-a", startedAtUtc));

        Assert.True(session.End(metadata!, endedAtUtc));

        session.RecordAlreadyEndedAudit(metadata!, auditOccurredAtUtc);

        var auditEvent = Assert.IsType<SessionEndAlreadyEndedAuditEvent>(session.Events[2]);
        Assert.Equal(auditOccurredAtUtc, auditEvent.OccurredAtUtc);
        Assert.NotEqual(default, auditEvent.OccurredAtUtc);
        Assert.NotNull(session.EndedAtUtc);
        Assert.True(auditEvent.OccurredAtUtc >= session.EndedAtUtc.Value);
    }

    [Fact]
    public void EndSession_ShouldReturnAlreadyEnded_WhenSessionAlreadyEnded_WithSystemMetadata()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var first = module.EndSession(new EndSessionCommand(start.SessionId!.Value, "operator-b", "Operator", "completed"));
        var beforeRetryAttemptUtc = DateTime.UtcNow;
        var second = module.EndSession(new EndSessionCommand(start.SessionId.Value, " system-a ", "sYsTeM", "   "));
        var afterRetryAttemptUtc = DateTime.UtcNow;
        var session = module.GetSession(start.SessionId.Value);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal(EndSessionOutcome.AlreadyEnded, second.Outcome);
        Assert.Equal("Ended", second.Status);
        Assert.Equal(first.EndedAtUtc, second.EndedAtUtc);

        Assert.NotNull(session);
        Assert.Equal("Ended", session.Status);
        Assert.Equal(3, session.Events.Count);
        Assert.Single(session.Events.OfType<SessionEndedEvent>());
        Assert.Single(session.Events.OfType<SessionEndAlreadyEndedAuditEvent>());

        var auditEvent = Assert.IsType<SessionEndAlreadyEndedAuditEvent>(session.Events[2]);
        Assert.Equal("system-a", auditEvent.EndedBy);
        Assert.Equal("System", auditEvent.ActorType);
        Assert.Null(auditEvent.Reason);
        Assert.Equal("Ended", auditEvent.CurrentStatus);
        Assert.Equal(first.EndedAtUtc, auditEvent.ExistingEndedAtUtc);
        Assert.InRange(auditEvent.OccurredAtUtc, beforeRetryAttemptUtc, afterRetryAttemptUtc);
        Assert.NotNull(first.EndedAtUtc);
        Assert.True(auditEvent.OccurredAtUtc >= first.EndedAtUtc.Value);
    }
}
