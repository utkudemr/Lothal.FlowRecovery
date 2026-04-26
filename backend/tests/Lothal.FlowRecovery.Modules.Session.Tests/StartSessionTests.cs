using Lothal.FlowRecovery.Modules.Session;

namespace Lothal.FlowRecovery.Modules.Session.Tests;

public sealed class StartSessionTests
{
    [Fact]
    public void StartSession_ShouldReject_WhenFlowIdIsMissing()
    {
        var module = new SessionModule();

        var result = module.StartSession(new StartSessionCommand("   ", "operator-a"));

        Assert.False(result.Success);
        Assert.Equal("FlowId is required.", result.Error);
        Assert.Null(result.SessionId);
        Assert.Null(result.Notification);
    }

    [Fact]
    public void StartSession_ShouldReject_WhenStartedByIsMissing()
    {
        var module = new SessionModule();

        var flowId = $"flow-{Guid.NewGuid():N}";
        var result = module.StartSession(new StartSessionCommand(flowId, " "));

        Assert.False(result.Success);
        Assert.Equal("StartedBy is required.", result.Error);
        Assert.Equal(flowId, result.FlowId);
        Assert.Null(result.Notification);
    }

    [Fact]
    public void StartSession_ShouldRejectDuplicateActiveSession_ForSameFlowId()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";

        var first = module.StartSession(new StartSessionCommand(flowId, "operator-a"));
        var second = module.StartSession(new StartSessionCommand(flowId, "operator-b"));

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal("Active session already exists.", second.Error);
        Assert.Null(second.Notification);
    }

    [Fact]
    public void StartSession_ShouldCreateSession_WhenRequestIsValid()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";

        var result = module.StartSession(new StartSessionCommand(flowId, "operator-a"));
        var session = module.GetSession(result.SessionId!.Value);

        Assert.True(result.Success);
        Assert.NotNull(result.SessionId);
        Assert.Equal(flowId, result.FlowId);
        Assert.Equal("Active", result.Status);
        Assert.NotNull(result.StartedAtUtc);
        Assert.Null(result.Error);
        var notification = Assert.IsType<SessionStartedNotification>(result.Notification);
        Assert.Equal(result.SessionId, notification.SessionId);
        Assert.Equal(flowId, notification.FlowId);
        Assert.Equal("operator-a", notification.StartedBy);
        Assert.Equal(result.StartedAtUtc, notification.OccurredAtUtc);
        Assert.NotNull(session);
        var startedEvent = Assert.IsType<SessionStartedEvent>(session.Events[0]);
        Assert.Equal(result.SessionId, startedEvent.SessionId);
        Assert.Equal(flowId, startedEvent.FlowId);
        Assert.Equal("operator-a", startedEvent.StartedBy);
        Assert.Equal(result.StartedAtUtc, startedEvent.OccurredAtUtc);
    }
}
