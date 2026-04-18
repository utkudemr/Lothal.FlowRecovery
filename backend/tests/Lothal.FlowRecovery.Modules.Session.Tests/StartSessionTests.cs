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
    }

    [Fact]
    public void StartSession_ShouldCreateSession_WhenRequestIsValid()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";

        var result = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        Assert.True(result.Success);
        Assert.NotNull(result.SessionId);
        Assert.Equal(flowId, result.FlowId);
        Assert.Equal("Active", result.Status);
        Assert.NotNull(result.StartedAtUtc);
        Assert.Null(result.Error);
    }
}