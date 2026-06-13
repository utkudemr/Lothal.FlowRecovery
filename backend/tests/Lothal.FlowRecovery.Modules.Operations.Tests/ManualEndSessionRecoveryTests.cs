namespace Lothal.FlowRecovery.Modules.Operations.Tests;

using Lothal.FlowRecovery.Modules.Operations.Domain;
using Lothal.FlowRecovery.Modules.Session;

public class ManualEndSessionRecoveryTests
{
    [Fact]
    public void ManualEndSessionRecovery_EndsSessionAndRecordsAction()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);

        // Create a session
        var sessionResult = sessionModule.StartSession(new StartSessionCommand("flow-end-" + Guid.NewGuid(), "user-001"));
        var sessionId = sessionResult.SessionId!.Value;

        // Open a recovery case
        var recoveryCase = operationsModule.OpenRecoveryCase(sessionId, "operator-001", "Initial");

        // Act
        var result = operationsModule.ManualEndSessionRecovery(recoveryCase.Id, "operator-001", "Manual recovery");

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Error);

        // Verify session was ended
        var session = sessionModule.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.Equal("Ended", session.Status);
        Assert.NotNull(session.EndedAtUtc);

        // Verify recovery case recorded the action
        var updatedCase = operationsModule.GetRecoveryCase(recoveryCase.Id);
        Assert.NotNull(updatedCase);
        Assert.Equal(2, updatedCase.Events.Count); // RecoveryCaseOpened + RecoveryActionRecorded
    }

    [Fact]
    public void ManualEndSessionRecovery_FailsWhenRecoveryCaseNotFound()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);

        // Act
        var result = operationsModule.ManualEndSessionRecovery(Guid.NewGuid(), "operator-001", "Recovery attempt");

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public void ManualEndSessionRecovery_FailsWhenSessionAlreadyEnded()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);

        // Create and end a session
        var sessionResult = sessionModule.StartSession(new StartSessionCommand("flow-already-ended-" + Guid.NewGuid(), "user-001"));
        var sessionId = sessionResult.SessionId!.Value;
        sessionModule.EndSession(new EndSessionCommand(sessionId, "operator-initial", "Operator", "Initial end"));

        // Open recovery case on ended session
        var recoveryCase = operationsModule.OpenRecoveryCase(sessionId, "operator-001", "Trying to recover ended session");

        // Act
        var result = operationsModule.ManualEndSessionRecovery(recoveryCase.Id, "operator-001", "Recovery attempt");

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void ManualEndSessionRecovery_IsIdempotent()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);

        // Create a session
        var sessionResult = sessionModule.StartSession(new StartSessionCommand("flow-idempotent-" + Guid.NewGuid(), "user-001"));
        var sessionId = sessionResult.SessionId!.Value;

        // Open a recovery case
        var recoveryCase = operationsModule.OpenRecoveryCase(sessionId, "operator-001", "Initial");

        // Act - Call twice
        var firstResult = operationsModule.ManualEndSessionRecovery(recoveryCase.Id, "operator-001", "First attempt");
        var secondResult = operationsModule.ManualEndSessionRecovery(recoveryCase.Id, "operator-002", "Second attempt");

        // Assert - First succeeds, second fails (already ended)
        Assert.True(firstResult.Success);
        Assert.False(secondResult.Success);

        // Verify session is ended only once
        var session = sessionModule.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.Equal("Ended", session.Status);
        Assert.NotNull(session.EndedAtUtc);
    }

    [Fact]
    public void ManualEndSessionRecovery_PreservesOperatorMetadata()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);

        // Create a session
        var sessionResult = sessionModule.StartSession(new StartSessionCommand("flow-metadata-" + Guid.NewGuid(), "user-001"));
        var sessionId = sessionResult.SessionId!.Value;

        // Open a recovery case
        var recoveryCase = operationsModule.OpenRecoveryCase(sessionId, "operator-recovery", "Initial");

        // Act
        var operatorId = "operator-manual";
        var reason = "Manual EndSession with operator metadata";
        var result = operationsModule.ManualEndSessionRecovery(recoveryCase.Id, operatorId, reason);

        // Assert
        Assert.True(result.Success);

        // Verify recovery case has action recorded with operator metadata
        var updatedCase = operationsModule.GetRecoveryCase(recoveryCase.Id);
        Assert.NotNull(updatedCase);
        var actionEvent = updatedCase.Events.OfType<RecoveryActionRecorded>().FirstOrDefault();
        Assert.NotNull(actionEvent);
        Assert.Equal(operatorId, actionEvent.OperatorId);
        Assert.Equal(reason, actionEvent.Reason);
    }
}
