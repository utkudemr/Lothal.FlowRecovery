using Lothal.FlowRecovery.Modules.Session;

namespace Lothal.FlowRecovery.Modules.Session.Tests;

public sealed class ListActiveSessionsTests
{
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
