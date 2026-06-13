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
        var openResult = operationsModule.OpenRecoveryCase(sessionId, DateTime.UtcNow.AddSeconds(1), "operator-001", "Initial");
        var recoveryCase = Assert.IsType<RecoveryCase>(openResult.RecoveryCase);

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
        Assert.Equal(RecoveryCaseStatus.Resolved, updatedCase.Status);
        Assert.Equal(4, updatedCase.Events.Count);
        Assert.IsType<RecoveryCaseOpened>(updatedCase.Events[0]);
        Assert.IsType<RecoveryCaseStatusChanged>(updatedCase.Events[1]);
        Assert.IsType<RecoveryActionRecorded>(updatedCase.Events[2]);
        Assert.IsType<RecoveryCaseStatusChanged>(updatedCase.Events[3]);
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
    public void ManualEndSessionRecovery_RejectsEmptyRecoveryId()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);

        // Act
        var result = operationsModule.ManualEndSessionRecovery(Guid.Empty, "operator-001", "Recovery attempt");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("RecoveryId is required.", result.Error);
    }

    [Theory]
    [InlineData(null, "Recovery attempt", "OperatorId is required.")]
    [InlineData(" ", "Recovery attempt", "OperatorId is required.")]
    [InlineData("operator-001", null, "Reason is required.")]
    [InlineData("operator-001", " ", "Reason is required.")]
    public void ManualEndSessionRecovery_RejectsMissingOperatorMetadata(
        string? operatorId,
        string? reason,
        string expectedError)
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);
        var sessionResult = sessionModule.StartSession(new StartSessionCommand("flow-boundary-" + Guid.NewGuid(), "user-001"));
        var openResult = operationsModule.OpenRecoveryCase(sessionResult.SessionId!.Value, DateTime.UtcNow.AddSeconds(1), "operator-001", "Initial");
        var recoveryCase = Assert.IsType<RecoveryCase>(openResult.RecoveryCase);

        // Act
        var result = operationsModule.ManualEndSessionRecovery(recoveryCase.Id, operatorId!, reason!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(expectedError, result.Error);

        var session = sessionModule.GetSession(recoveryCase.SessionId);
        Assert.NotNull(session);
        Assert.Equal("Active", session.Status);

        var updatedCase = operationsModule.GetRecoveryCase(recoveryCase.Id);
        Assert.NotNull(updatedCase);
        Assert.Single(updatedCase.Events);
    }

    [Fact]
    public void ManualEndSessionRecovery_SucceedsWhenSessionAlreadyEndedAndRecordsAudit()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);

        // Create and end a session
        var sessionResult = sessionModule.StartSession(new StartSessionCommand("flow-already-ended-" + Guid.NewGuid(), "user-001"));
        var sessionId = sessionResult.SessionId!.Value;

        // Open recovery case while active, then simulate the session being ended before the recovery action runs.
        var openResult = operationsModule.OpenRecoveryCase(sessionId, DateTime.UtcNow.AddSeconds(1), "operator-001", "Trying to recover session");
        var recoveryCase = Assert.IsType<RecoveryCase>(openResult.RecoveryCase);
        sessionModule.EndSession(new EndSessionCommand(sessionId, "operator-initial", "Operator", "Initial end"));

        // Act
        var result = operationsModule.ManualEndSessionRecovery(recoveryCase.Id, "operator-001", "Recovery attempt");

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Error);

        var session = sessionModule.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.Equal("Ended", session.Status);
        Assert.Single(session.Events.OfType<SessionEndedEvent>());
        Assert.Single(session.Events.OfType<SessionEndAlreadyEndedAuditEvent>());

        var updatedCase = operationsModule.GetRecoveryCase(recoveryCase.Id);
        Assert.NotNull(updatedCase);
        Assert.Equal(RecoveryCaseStatus.Resolved, updatedCase.Status);
        var actionEvent = Assert.Single(updatedCase.Events.OfType<RecoveryActionRecorded>());
        Assert.Equal("EndSessionAlreadyEnded", actionEvent.ActionName);
        Assert.Equal("operator-001", actionEvent.OperatorId);
        Assert.Equal("Recovery attempt", actionEvent.Reason);
    }

    [Fact]
    public void ManualEndSessionRecovery_IsIdempotentAndAuditsRepeatedAttempt()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);

        // Create a session
        var sessionResult = sessionModule.StartSession(new StartSessionCommand("flow-idempotent-" + Guid.NewGuid(), "user-001"));
        var sessionId = sessionResult.SessionId!.Value;

        // Open a recovery case
        var openResult = operationsModule.OpenRecoveryCase(sessionId, DateTime.UtcNow.AddSeconds(1), "operator-001", "Initial");
        var recoveryCase = Assert.IsType<RecoveryCase>(openResult.RecoveryCase);

        // Act - Call twice
        var firstResult = operationsModule.ManualEndSessionRecovery(recoveryCase.Id, "operator-001", "First attempt");
        var secondResult = operationsModule.ManualEndSessionRecovery(recoveryCase.Id, "operator-002", "Second attempt");

        // Assert - Both attempts succeed; the second records an idempotent audit instead of ending again.
        Assert.True(firstResult.Success);
        Assert.True(secondResult.Success);
        Assert.Null(secondResult.Error);

        var session = sessionModule.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.Equal("Ended", session.Status);
        Assert.NotNull(session.EndedAtUtc);
        Assert.Single(session.Events.OfType<SessionEndedEvent>());
        Assert.Single(session.Events.OfType<SessionEndAlreadyEndedAuditEvent>());

        var updatedCase = operationsModule.GetRecoveryCase(recoveryCase.Id);
        Assert.NotNull(updatedCase);
        Assert.Equal(RecoveryCaseStatus.Resolved, updatedCase.Status);
        var actionEvents = updatedCase.Events.OfType<RecoveryActionRecorded>().ToArray();
        Assert.Equal(2, actionEvents.Length);
        Assert.Equal("EndSession", actionEvents[0].ActionName);
        Assert.Equal("operator-001", actionEvents[0].OperatorId);
        Assert.Equal("First attempt", actionEvents[0].Reason);
        Assert.Equal("EndSessionAlreadyEnded", actionEvents[1].ActionName);
        Assert.Equal("operator-002", actionEvents[1].OperatorId);
        Assert.Equal("Second attempt", actionEvents[1].Reason);
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
        var openResult = operationsModule.OpenRecoveryCase(sessionId, DateTime.UtcNow.AddSeconds(1), "operator-recovery", "Initial");
        var recoveryCase = Assert.IsType<RecoveryCase>(openResult.RecoveryCase);

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
