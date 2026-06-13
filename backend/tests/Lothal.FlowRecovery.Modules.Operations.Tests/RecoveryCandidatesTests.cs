namespace Lothal.FlowRecovery.Modules.Operations.Tests;

using Lothal.FlowRecovery.Modules.Session;
public class RecoveryCandidatesTests
{
    [Fact]
    public void GetRecoveryCandidates_IncludesExpectedStaleActiveSession()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);
        var flowId = "flow-stale-" + Guid.NewGuid().ToString("N");
        var sessionId = StartSession(sessionModule, flowId);
        var threshold = sessionModule.GetSession(sessionId)!.LastEventAtUtc;

        // Act
        var candidates = operationsModule.GetRecoveryCandidates(threshold);

        // Assert
        var candidate = Assert.Single(candidates, candidate => candidate.SessionId == sessionId);
        Assert.Equal(flowId, candidate.FlowId);
        Assert.Equal("unknown", candidate.CurrentStep);
        Assert.Equal(threshold, candidate.LastEventAtUtc);
    }

    [Fact]
    public void GetRecoveryCandidates_ExcludesNonStaleActiveSession()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);
        var staleSessionId = StartSession(sessionModule, "flow-stale-" + Guid.NewGuid().ToString("N"));
        var threshold = sessionModule.GetSession(staleSessionId)!.LastEventAtUtc;

        Assert.True(SpinWait.SpinUntil(() => DateTime.UtcNow > threshold, TimeSpan.FromSeconds(1)));
        var nonStaleSessionId = StartSession(sessionModule, "flow-non-stale-" + Guid.NewGuid().ToString("N"));

        // Act
        var candidates = operationsModule.GetRecoveryCandidates(threshold);

        // Assert
        Assert.Contains(candidates, candidate => candidate.SessionId == staleSessionId);
        Assert.DoesNotContain(candidates, candidate => candidate.SessionId == nonStaleSessionId);
    }

    [Fact]
    public void GetRecoveryCandidates_ExcludesEndedSession()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);
        var endedSessionId = StartSession(sessionModule, "flow-ended-" + Guid.NewGuid().ToString("N"));
        var threshold = sessionModule.GetSession(endedSessionId)!.LastEventAtUtc;
        var end = sessionModule.EndSession(new EndSessionCommand(endedSessionId, "operator-001", "Operator", "No longer recoverable"));
        Assert.True(end.Success);

        // Act
        var candidates = operationsModule.GetRecoveryCandidates(threshold);

        // Assert
        Assert.DoesNotContain(candidates, candidate => candidate.SessionId == endedSessionId);
    }

    private static Guid StartSession(SessionModule sessionModule, string flowId)
    {
        var result = sessionModule.StartSession(new StartSessionCommand(flowId, "user-001"));
        Assert.True(result.Success);
        Assert.NotNull(result.SessionId);
        return result.SessionId.Value;
    }
}
