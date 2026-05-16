using Lothal.FlowRecovery.Modules.Session;

namespace Lothal.FlowRecovery.Modules.Session.Tests;

public sealed class ListActiveSessionsTests
{
    [Fact]
    public void GetActiveSessionByFlowId_ShouldReturnActiveSession_WhenFlowIdHasWhitespaceAndDifferentCase()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var snapshot = module.GetActiveSessionByFlowId($"  {flowId.ToUpperInvariant()}  ");

        Assert.NotNull(snapshot);
        Assert.Equal(start.SessionId, snapshot.SessionId);
        Assert.Equal(flowId, snapshot.FlowId);
        Assert.Equal("Active", snapshot.Status);
    }

    [Fact]
    public void GetActiveSessionByFlowId_ShouldReturnNull_WhenSessionIsEnded()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));
        var end = module.EndSession(new EndSessionCommand(start.SessionId!.Value, "operator-a", "Operator", "done"));

        var snapshot = module.GetActiveSessionByFlowId(flowId);

        Assert.True(end.Success);
        Assert.Null(snapshot);
    }

    [Fact]
    public void GetSession_ShouldReturnSnapshotWithLastEventTypeFromLatestEvent()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = SessionWorkflowTestDefinitions.CreateModule(flowId);

        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));
        var setResult = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "cart", "operator-b", "System", null));

        var snapshot = module.GetSession(start.SessionId!.Value);

        Assert.True(setResult.Success);
        Assert.NotNull(snapshot);
        Assert.Equal(nameof(SessionCurrentStepSetEvent), snapshot!.LastEventType);
        Assert.Equal(snapshot.Events[^1].OccurredAtUtc, snapshot.LastEventAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetActiveSessionByFlowId_ShouldReturnNull_WhenFlowIdIsBlank(string flowId)
    {
        var module = new SessionModule();

        var snapshot = module.GetActiveSessionByFlowId(flowId);

        Assert.Null(snapshot);
    }

    [Fact]
    public void ListActiveSessions_ShouldReturnOnlyActiveSessionSnapshots()
    {
        var module = new SessionModule();
        var firstFlowId = $"flow-{Guid.NewGuid():N}";
        var secondFlowId = $"flow-{Guid.NewGuid():N}";

        var firstStart = module.StartSession(new StartSessionCommand(firstFlowId, "operator-a"));
        var secondStart = module.StartSession(new StartSessionCommand(secondFlowId, "operator-b"));

        var sessions = module.ListActiveSessions();

        Assert.Contains(sessions, session => session.SessionId == firstStart.SessionId);
        Assert.Contains(sessions, session => session.SessionId == secondStart.SessionId);
        Assert.All(sessions, session => Assert.Equal("Active", session.Status));
    }

    [Fact]
    public async Task ListActiveSessions_ShouldReturnSessionsOrderedByStartedAtUtc()
    {
        var module = new SessionModule();
        var firstFlowId = $"flow-{Guid.NewGuid():N}";
        var secondFlowId = $"flow-{Guid.NewGuid():N}";

        var firstStart = module.StartSession(new StartSessionCommand(firstFlowId, "operator-a"));
        await Task.Delay(5);
        var secondStart = module.StartSession(new StartSessionCommand(secondFlowId, "operator-b"));

        var sessions = module.ListActiveSessions();
        var firstIndex = sessions
            .Select((session, index) => new { session, index })
            .Single(item => item.session.SessionId == firstStart.SessionId!.Value)
            .index;
        var secondIndex = sessions
            .Select((session, index) => new { session, index })
            .Single(item => item.session.SessionId == secondStart.SessionId!.Value)
            .index;

        Assert.True(firstIndex < secondIndex);
        Assert.True(
            sessions[firstIndex].StartedAtUtc <= sessions[secondIndex].StartedAtUtc,
            "Sessions should be ordered by StartedAtUtc ascending.");
    }

    [Fact]
    public void ListActiveSessions_ShouldExcludeEndedSessions()
    {
        var module = new SessionModule();
        var activeFlowId = $"flow-{Guid.NewGuid():N}";
        var endedFlowId = $"flow-{Guid.NewGuid():N}";

        var activeStart = module.StartSession(new StartSessionCommand(activeFlowId, "operator-a"));
        var endedStart = module.StartSession(new StartSessionCommand(endedFlowId, "operator-b"));

        var endResult = module.EndSession(new EndSessionCommand(endedStart.SessionId!.Value, "system", "System", "done"));
        var sessions = module.ListActiveSessions();

        Assert.True(endResult.Success);
        Assert.Contains(sessions, session => session.SessionId == activeStart.SessionId);
        Assert.DoesNotContain(sessions, session => session.SessionId == endedStart.SessionId);
        Assert.All(sessions, session => Assert.Equal("Active", session.Status));
    }

    [Fact]
    public void ListStaleActiveSessions_ShouldIncludeThresholdMatch_ExcludeNewerAndEndedSessions()
    {
        var store = new InMemorySessionStore();
        var module = new SessionModule(store);
        var threshold = new DateTime(2026, 5, 16, 9, 30, 0, DateTimeKind.Utc);
        var matchedEventAtUtc = threshold;
        var newerEventAtUtc = threshold.AddMinutes(1);

        var firstSessionId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var newerSessionId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var endedSessionId = Guid.Parse("00000000-0000-0000-0000-000000000003");

        Assert.True(store.TrySaveIfNoActiveSession(firstSessionId, "flow-a", "operator-a", matchedEventAtUtc, "operator-a", out _, out _, out _));
        Assert.True(store.TrySaveIfNoActiveSession(newerSessionId, "flow-b", "operator-b", newerEventAtUtc, "operator-b", out _, out _, out _));
        Assert.True(store.TrySaveIfNoActiveSession(endedSessionId, "flow-c", "operator-c", matchedEventAtUtc, "operator-c", out _, out _, out _));
        Assert.True(module.EndSession(new EndSessionCommand(endedSessionId, "system", "System", "done")).Success);

        var sessions = module.ListStaleActiveSessions(threshold);

        var staleSession = Assert.Single(sessions);
        Assert.Equal(firstSessionId, staleSession.SessionId);
        Assert.Equal(threshold, staleSession.LastEventAtUtc);
        Assert.DoesNotContain(sessions, session => session.SessionId == newerSessionId);
        Assert.All(sessions, session => Assert.Equal("Active", session.Status));
        Assert.All(sessions, session => Assert.True(session.LastEventAtUtc <= threshold));
        Assert.DoesNotContain(sessions, session => session.SessionId == endedSessionId);
    }

    [Fact]
    public void ListStaleActiveSessions_ShouldOrderByLastEventAtUtcThenSessionId()
    {
        var store = new InMemorySessionStore();
        var module = new SessionModule(store);
        var threshold = new DateTime(2026, 5, 16, 9, 30, 0, DateTimeKind.Utc);
        var oldestEventAtUtc = threshold.AddMinutes(-2);
        var tiedEventAtUtc = threshold.AddMinutes(-1);

        var oldestSessionId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var tiedFirstSessionId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var tiedSecondSessionId = Guid.Parse("00000000-0000-0000-0000-000000000002");

        Assert.True(store.TrySaveIfNoActiveSession(oldestSessionId, "flow-a", "operator-a", oldestEventAtUtc, "operator-a", out _, out _, out _));
        Assert.True(store.TrySaveIfNoActiveSession(tiedSecondSessionId, "flow-b", "operator-b", tiedEventAtUtc, "operator-b", out _, out _, out _));
        Assert.True(store.TrySaveIfNoActiveSession(tiedFirstSessionId, "flow-c", "operator-c", tiedEventAtUtc, "operator-c", out _, out _, out _));

        var sessions = module.ListStaleActiveSessions(threshold);

        Assert.Equal(
            new[]
            {
                oldestSessionId,
                tiedFirstSessionId,
                tiedSecondSessionId,
            },
            sessions.Select(session => session.SessionId));
        Assert.All(sessions, session => Assert.Equal("Active", session.Status));
        Assert.All(sessions, session => Assert.True(session.LastEventAtUtc <= threshold));
        Assert.Equal(oldestEventAtUtc, sessions[0].LastEventAtUtc);
        Assert.Equal(tiedEventAtUtc, sessions[1].LastEventAtUtc);
        Assert.Equal(tiedEventAtUtc, sessions[2].LastEventAtUtc);
    }

    [Fact]
    public void ListStaleActiveSessions_ShouldExcludeActiveSessionsWithLastEventAtUtcNewerThanThreshold()
    {
        var store = new InMemorySessionStore();
        var module = new SessionModule(store);
        var threshold = new DateTime(2026, 5, 16, 9, 30, 0, DateTimeKind.Utc);
        var staleEventAtUtc = threshold;
        var freshEventAtUtc = threshold.AddMinutes(1);

        var staleSessionId = Guid.Parse("00000000-0000-0000-0000-000000000010");
        var freshSessionId = Guid.Parse("00000000-0000-0000-0000-000000000011");

        Assert.True(store.TrySaveIfNoActiveSession(staleSessionId, "flow-a", "operator-a", staleEventAtUtc, "operator-a", out _, out _, out _));
        Assert.True(store.TrySaveIfNoActiveSession(freshSessionId, "flow-b", "operator-b", freshEventAtUtc, "operator-b", out _, out _, out _));

        var sessions = module.ListStaleActiveSessions(threshold);

        Assert.Single(sessions);
        Assert.Equal(staleSessionId, sessions[0].SessionId);
        Assert.Equal(staleEventAtUtc, sessions[0].LastEventAtUtc);
        Assert.All(sessions, session => Assert.Equal("Active", session.Status));
        Assert.All(sessions, session => Assert.True(session.LastEventAtUtc <= threshold));
    }

    [Fact]
    public void ListStaleActiveSessions_ShouldExcludeEndedSessions()
    {
        var module = new SessionModule();
        var activeFlowId = $"flow-{Guid.NewGuid():N}";
        var endedFlowId = $"flow-{Guid.NewGuid():N}";

        var activeStart = module.StartSession(new StartSessionCommand(activeFlowId, "operator-a"));
        var endedStart = module.StartSession(new StartSessionCommand(endedFlowId, "operator-b"));
        var endResult = module.EndSession(new EndSessionCommand(endedStart.SessionId!.Value, "system", "System", "done"));

        var endedSnapshot = module.GetSession(endedStart.SessionId!.Value);
        var threshold = endedSnapshot!.LastEventAtUtc;
        var sessions = module.ListStaleActiveSessions(threshold);

        Assert.True(activeStart.Success);
        Assert.True(endedStart.Success);
        Assert.True(endResult.Success);
        Assert.Contains(sessions, session => session.SessionId == activeStart.SessionId);
        Assert.DoesNotContain(sessions, session => session.SessionId == endedStart.SessionId);
        Assert.All(sessions, session => Assert.Equal("Active", session.Status));
        Assert.All(sessions, session => Assert.True(session.LastEventAtUtc <= threshold));
    }

    [Fact]
    public void ListActiveSessions_ShouldReflectUpdatedCurrentStep_AndPreserveEventHistory_AfterSetCurrentStep()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = SessionWorkflowTestDefinitions.CreateModule(flowId);

        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));
        var beforeSetUtc = DateTime.UtcNow;
        var setResult = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "cart", "operator-b", "System", null));
        var afterSetUtc = DateTime.UtcNow;

        var sessions = module.ListActiveSessions();
        var snapshot = Assert.Single(sessions, session => session.SessionId == start.SessionId);

        Assert.True(setResult.Success);
        Assert.Equal("cart", setResult.CurrentStep);
        Assert.Equal("cart", snapshot.CurrentStep);
        Assert.Equal(snapshot.Events[^1].OccurredAtUtc, snapshot.LastEventAtUtc);
        Assert.Equal(nameof(SessionCurrentStepSetEvent), snapshot.LastEventType);
        Assert.InRange(snapshot.LastEventAtUtc, beforeSetUtc, afterSetUtc);
        Assert.Equal(2, snapshot.Events.Count);

        var startedEvent = Assert.IsType<SessionStartedEvent>(snapshot.Events[0]);
        var stepEvent = Assert.IsType<SessionCurrentStepSetEvent>(snapshot.Events[1]);

        Assert.Equal(start.SessionId, startedEvent.SessionId);
        Assert.Equal(flowId, startedEvent.FlowId);
        Assert.Equal("cart", stepEvent.CurrentStep);
        Assert.Null(stepEvent.PreviousStep);
        Assert.Equal("operator-b", stepEvent.ChangedBy);
        Assert.Equal("System", stepEvent.ActorType);
        Assert.InRange(stepEvent.OccurredAtUtc, beforeSetUtc, afterSetUtc);
    }

}
