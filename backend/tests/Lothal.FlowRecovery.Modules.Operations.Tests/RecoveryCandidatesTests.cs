namespace Lothal.FlowRecovery.Modules.Operations.Tests;

using Lothal.FlowRecovery.Modules.Session;
using Lothal.FlowRecovery.Modules.Workflow;

public class RecoveryCandidatesTests
{
    [Fact]
    public void GetRecoveryCandidates_ReturnsMatchingCandidates()
    {
        // Arrange - Use a workflow to enable SetCurrentStep
        var workflowProvider = new InMemoryWorkflowDefinitionProvider(
            new WorkflowDefinition(
                "flow-test",
                new[] { "step1", "step2", "step3" },
                new Dictionary<string, IReadOnlyCollection<string>>
                {
                    ["step1"] = new[] { "step2" },
                    ["step2"] = new[] { "step3" },
                    ["step3"] = Array.Empty<string>(),
                }));
        var sessionModule = new SessionModule(workflowProvider);
        var operationsModule = new OperationsModule(sessionModule);

        // Create a session
        var flowId = "flow-test-" + Guid.NewGuid().ToString("N");
        var result = sessionModule.StartSession(new StartSessionCommand(flowId, "user-001"));
        var sessionId = result.SessionId!.Value;

        // Set the stale threshold to just after "now"
        var staleBeforeUtc = DateTime.UtcNow.AddSeconds(1);

        // Act
        var candidates = operationsModule.GetRecoveryCandidates(staleBeforeUtc);

        // Assert - Should have at least one candidate from the session we just created
        Assert.NotEmpty(candidates);
        var ourCandidate = candidates.FirstOrDefault(c => c.SessionId == sessionId);
        Assert.NotNull(ourCandidate);
        Assert.Equal(flowId, ourCandidate.FlowId);
        Assert.True(ourCandidate.LastEventAtUtc <= staleBeforeUtc);
    }

    [Fact]
    public void GetRecoveryCandidates_HandlesSessionWithoutWorkflowDefinition()
    {
        // Arrange - Create session module without workflow provider
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);

        // Create a session for a flow with no workflow definition
        var flowId = "unknown-flow-" + Guid.NewGuid().ToString("N");
        var result = sessionModule.StartSession(new StartSessionCommand(flowId, "user-001"));
        var sessionId = result.SessionId!.Value;

        var staleBeforeUtc = DateTime.UtcNow.AddSeconds(1);

        // Act
        var candidates = operationsModule.GetRecoveryCandidates(staleBeforeUtc);

        // Assert
        var ourCandidate = candidates.FirstOrDefault(c => c.SessionId == sessionId);
        Assert.NotNull(ourCandidate);
        Assert.Equal("unknown", ourCandidate.CurrentStep);
    }

    [Fact]
    public void GetRecoveryCandidates_FiltersByStaleThreshold()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);

        // Create a session
        var flowId = "flow-future-" + Guid.NewGuid().ToString("N");
        sessionModule.StartSession(new StartSessionCommand(flowId, "user-001"));

        // Set stale threshold to the past (very old), so all active sessions are included
        var staleBeforeUtc = DateTime.UtcNow.AddSeconds(1);

        // Act
        var candidates = operationsModule.GetRecoveryCandidates(staleBeforeUtc);

        // Assert
        Assert.NotEmpty(candidates);
        Assert.All(candidates, c => Assert.True(c.LastEventAtUtc <= staleBeforeUtc));
    }
}
