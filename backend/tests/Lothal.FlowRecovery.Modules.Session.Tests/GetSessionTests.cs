using Lothal.FlowRecovery.Modules.Session;

namespace Lothal.FlowRecovery.Modules.Session.Tests;

public sealed class GetSessionTests
{
    [Fact]
    public void GetSession_ShouldReturnData_WhenSessionExists()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";

        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));
        var session = module.GetSession(start.SessionId!.Value);

        Assert.NotNull(session);
        Assert.Equal(start.SessionId, session.SessionId);
        Assert.Equal(flowId, session.FlowId);
        Assert.Equal("operator-a", session.StartedBy);
        Assert.Equal("Active", session.Status);
        Assert.Equal(start.StartedAtUtc, session.StartedAtUtc);
        Assert.Single(session.Events);

        var startedEvent = Assert.IsType<SessionStartedEvent>(session.Events[0]);
        Assert.Equal(flowId, startedEvent.FlowId);
        Assert.Equal("operator-a", startedEvent.StartedBy);
    }

    [Fact]
    public void GetSession_ShouldBeVisibleAcrossModuleInstances_WhenUsingSharedStore()
    {
        var firstModule = new SessionModule();
        var secondModule = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";

        var start = firstModule.StartSession(new StartSessionCommand(flowId, "operator-a"));
        var session = secondModule.GetSession(start.SessionId!.Value);

        Assert.NotNull(session);
        Assert.Equal(start.SessionId, session.SessionId);
        Assert.Equal(flowId, session.FlowId);
        Assert.Equal("operator-a", session.StartedBy);
        Assert.Equal("Active", session.Status);
        Assert.Equal(start.StartedAtUtc, session.StartedAtUtc);
        Assert.Single(session.Events);

        var startedEvent = Assert.IsType<SessionStartedEvent>(session.Events[0]);
        Assert.Equal(flowId, startedEvent.FlowId);
        Assert.Equal("operator-a", startedEvent.StartedBy);
    }

    [Fact]
    public void GetSession_ShouldReturnIndependentEventSnapshots_OnEachRead()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";

        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));
        var firstRead = module.GetSession(start.SessionId!.Value);

        Assert.NotNull(firstRead);
        Assert.Single(firstRead.Events);

        if (firstRead.Events is SessionEvent[] eventsArray && eventsArray.Length > 0)
        {
            eventsArray[0] = new SessionStartedEvent(
                "flow-mutated",
                "operator-mutated",
                firstRead.StartedAtUtc.AddMinutes(1));
        }

        var secondRead = module.GetSession(start.SessionId.Value);

        Assert.NotNull(secondRead);
        Assert.Equal(start.SessionId, secondRead.SessionId);
        Assert.Equal(flowId, secondRead.FlowId);
        Assert.Equal("operator-a", secondRead.StartedBy);
        Assert.Equal("Active", secondRead.Status);
        Assert.Equal(start.StartedAtUtc, secondRead.StartedAtUtc);
        Assert.Single(secondRead.Events);
        Assert.NotSame(firstRead.Events, secondRead.Events);

        var startedEvent = Assert.IsType<SessionStartedEvent>(secondRead.Events[0]);
        Assert.Equal(flowId, startedEvent.FlowId);
        Assert.Equal("operator-a", startedEvent.StartedBy);
        Assert.Equal(start.StartedAtUtc, startedEvent.OccurredAtUtc);
    }

    [Fact]
    public void GetSession_ShouldReturnNull_WhenSessionDoesNotExist()
    {
        var module = new SessionModule();

        var session = module.GetSession(Guid.NewGuid());

        Assert.Null(session);
    }

}
