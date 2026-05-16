using System.Reflection;
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
    public void ListStaleActiveSessions_ShouldReturnOnlyActiveSessionsOrderedByLastEventAtUtcThenSessionId()
    {
        var module = new SessionModule();
        var firstFlowId = $"flow-{Guid.NewGuid():N}";
        var secondFlowId = $"flow-{Guid.NewGuid():N}";
        var endedFlowId = $"flow-{Guid.NewGuid():N}";
        var staleBeforeUtc = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var firstStaleLastEventAtUtc = staleBeforeUtc.AddMinutes(-2);
        var secondStaleLastEventAtUtc = staleBeforeUtc.AddMinutes(-1);

        var firstStart = module.StartSession(new StartSessionCommand(firstFlowId, "operator-a"));
        var secondStart = module.StartSession(new StartSessionCommand(secondFlowId, "operator-b"));

        SetSessionLastEventAtUtc(firstStart.SessionId!.Value, firstStaleLastEventAtUtc);
        SetSessionLastEventAtUtc(secondStart.SessionId!.Value, secondStaleLastEventAtUtc);

        var endedStart = module.StartSession(new StartSessionCommand(endedFlowId, "operator-c"));
        var endResult = module.EndSession(new EndSessionCommand(endedStart.SessionId!.Value, "system", "System", "done"));

        var sessions = module.ListStaleActiveSessions(staleBeforeUtc);
        var staleSessions = sessions
            .Where(session => session.SessionId == firstStart.SessionId || session.SessionId == secondStart.SessionId)
            .ToArray();

        Assert.True(endResult.Success);
        Assert.Equal(new[] { firstStart.SessionId!.Value, secondStart.SessionId!.Value }, sessions.Select(session => session.SessionId));
        Assert.Equal(2, staleSessions.Length);
        Assert.Equal(firstStart.SessionId, staleSessions[0].SessionId);
        Assert.Equal(secondStart.SessionId, staleSessions[1].SessionId);
        Assert.Equal(firstStaleLastEventAtUtc, staleSessions[0].LastEventAtUtc);
        Assert.Equal(secondStaleLastEventAtUtc, staleSessions[1].LastEventAtUtc);
        Assert.All(staleSessions, session => Assert.Equal("Active", session.Status));
        Assert.DoesNotContain(sessions, session => session.SessionId == endedStart.SessionId);
    }

    [Fact]
    public void ListStaleActiveSessions_ShouldExcludeActiveSessionsWithLastEventAtUtcNewerThanThreshold()
    {
        var module = new SessionModule();
        var staleFlowId = $"flow-{Guid.NewGuid():N}";
        var freshFlowId = $"flow-{Guid.NewGuid():N}";
        var staleBeforeUtc = DateTime.UtcNow.AddMinutes(-1);
        var staleLastEventAtUtc = staleBeforeUtc.AddSeconds(-1);
        var newerLastEventAtUtc = staleBeforeUtc.AddSeconds(1);

        var staleStart = module.StartSession(new StartSessionCommand(staleFlowId, "operator-a"));
        var freshStart = module.StartSession(new StartSessionCommand(freshFlowId, "operator-b"));

        SetSessionLastEventAtUtc(staleStart.SessionId!.Value, staleLastEventAtUtc);
        SetSessionLastEventAtUtc(freshStart.SessionId!.Value, newerLastEventAtUtc);

        var sessions = module.ListStaleActiveSessions(staleBeforeUtc);

        Assert.Contains(sessions, session => session.SessionId == staleStart.SessionId && session.LastEventAtUtc < staleBeforeUtc);
        Assert.DoesNotContain(sessions, session => session.SessionId == freshStart.SessionId);
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

    private static void SetSessionLastEventAtUtc(Guid sessionId, DateTime occurredAtUtc)
    {
        var sharedStore = typeof(SessionModule)
            .GetField("SharedStore", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        var syncField = sharedStore.GetType().GetField("_sync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var sync = syncField.GetValue(sharedStore)!;

        lock (sync)
        {
            var sessionsField = sharedStore.GetType().GetField("_sessions", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var sessions = (Dictionary<Guid, SessionRecord>)sessionsField.GetValue(sharedStore)!;
            var session = sessions[sessionId];

            var eventsField = typeof(SessionRecord).GetField("_events", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var events = (List<SessionEvent>)eventsField.GetValue(session)!;
            events[^1] = RecreateEventWithOccurredAt(events[^1], occurredAtUtc);
        }
    }

    private static SessionEvent RecreateEventWithOccurredAt(SessionEvent sessionEvent, DateTime occurredAtUtc) =>
        sessionEvent switch
        {
            SessionStartedEvent started => started with { OccurredAtUtc = occurredAtUtc },
            _ => throw new InvalidOperationException("Test helper expected the last event to be a start event."),
        };
}
