namespace Lothal.FlowRecovery.Modules.Operations.Tests;

using Lothal.FlowRecovery.Modules.Operations.Domain;

public class RecoveryCaseTests
{
    [Fact]
    public void NewRecoveryCase_HasCorrectInitialState()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var operatorId = "operator-001";
        var reason = "Stale session detected";

        // Act
        var recoveryCase = new RecoveryCase(Guid.NewGuid(), sessionId, operatorId, reason);

        // Assert
        Assert.NotEqual(Guid.Empty, recoveryCase.Id);
        Assert.Equal(sessionId, recoveryCase.SessionId);
        Assert.Equal(operatorId, recoveryCase.CreatedByOperatorId);
        Assert.Equal(RecoveryCaseStatus.New, recoveryCase.Status);
        Assert.True(recoveryCase.CreatedAtUtc > DateTime.MinValue);
    }

    [Fact]
    public void NewRecoveryCase_CreatesOpenedEvent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var operatorId = "operator-001";
        var reason = "Stale session detected";
        var caseId = Guid.NewGuid();

        // Act
        var recoveryCase = new RecoveryCase(caseId, sessionId, operatorId, reason);

        // Assert
        Assert.Single(recoveryCase.Events);
        var @event = recoveryCase.Events[0] as RecoveryCaseOpened;
        Assert.NotNull(@event);
        Assert.Equal(caseId, @event.RecoveryCaseId);
        Assert.Equal(sessionId, @event.SessionId);
        Assert.Equal(operatorId, @event.OperatorId);
        Assert.Equal(reason, @event.Reason);
    }

    [Fact]
    public void ChangeStatus_UpdatesStatusAndEmitsEvent()
    {
        // Arrange
        var recoveryCase = new RecoveryCase(Guid.NewGuid(), Guid.NewGuid(), "op-001", "Initial");
        var operatorId = "op-002";
        var reason = "Status change reason";

        // Act
        recoveryCase.ChangeStatus(RecoveryCaseStatus.InProgress, operatorId, reason);

        // Assert
        Assert.Equal(RecoveryCaseStatus.InProgress, recoveryCase.Status);
        Assert.Equal(2, recoveryCase.Events.Count);
        var statusChangeEvent = recoveryCase.Events[1] as RecoveryCaseStatusChanged;
        Assert.NotNull(statusChangeEvent);
        Assert.Equal(RecoveryCaseStatus.InProgress, statusChangeEvent.NewStatus);
        Assert.Equal(operatorId, statusChangeEvent.OperatorId);
        Assert.Equal(reason, statusChangeEvent.Reason);
    }

    [Fact]
    public void ChangeStatus_SameStatus_DoesNotEmitEvent()
    {
        // Arrange
        var recoveryCase = new RecoveryCase(Guid.NewGuid(), Guid.NewGuid(), "op-001", "Initial");

        // Act
        recoveryCase.ChangeStatus(RecoveryCaseStatus.New, "op-002", "Same status");

        // Assert
        Assert.Single(recoveryCase.Events); // Only the initial opened event
    }

    [Fact]
    public void ChangeStatus_RejectsInvalidTransition()
    {
        // Arrange
        var recoveryCase = new RecoveryCase(Guid.NewGuid(), Guid.NewGuid(), "op-001", "Initial");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => recoveryCase.ChangeStatus(RecoveryCaseStatus.Resolved, "op-002", "Cannot resolve before work starts"));

        // Assert
        Assert.Equal("Recovery case cannot transition from New to Resolved.", exception.Message);
        Assert.Equal(RecoveryCaseStatus.New, recoveryCase.Status);
        Assert.Single(recoveryCase.Events);
    }

    [Fact]
    public void ChangeStatus_RejectsTransitionFromTerminalStatus()
    {
        // Arrange
        var recoveryCase = new RecoveryCase(Guid.NewGuid(), Guid.NewGuid(), "op-001", "Initial");
        recoveryCase.ChangeStatus(RecoveryCaseStatus.InProgress, "op-002", "Starting recovery");
        recoveryCase.ChangeStatus(RecoveryCaseStatus.Resolved, "op-002", "Recovery complete");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => recoveryCase.ChangeStatus(RecoveryCaseStatus.InProgress, "op-003", "Reopen"));

        // Assert
        Assert.Equal("Recovery case cannot transition from Resolved to InProgress.", exception.Message);
        Assert.Equal(RecoveryCaseStatus.Resolved, recoveryCase.Status);
        Assert.Equal(3, recoveryCase.Events.Count);
    }

    [Fact]
    public void RecordAction_EmitsActionRecordedEvent()
    {
        // Arrange
        var recoveryCase = new RecoveryCase(Guid.NewGuid(), Guid.NewGuid(), "op-001", "Initial");
        var actionName = "EndSession";
        var operatorId = "op-002";
        var reason = "Ending stuck session";

        // Act
        recoveryCase.RecordAction(actionName, operatorId, reason);

        // Assert
        Assert.Equal(2, recoveryCase.Events.Count);
        var actionEvent = recoveryCase.Events[1] as RecoveryActionRecorded;
        Assert.NotNull(actionEvent);
        Assert.Equal(actionName, actionEvent.ActionName);
        Assert.Equal(operatorId, actionEvent.OperatorId);
        Assert.Equal(reason, actionEvent.Reason);
    }

    [Fact]
    public void RecordAction_RejectsResolvedRecoveryCase()
    {
        // Arrange
        var recoveryCase = new RecoveryCase(Guid.NewGuid(), Guid.NewGuid(), "op-001", "Initial");
        recoveryCase.ChangeStatus(RecoveryCaseStatus.InProgress, "op-002", "Starting recovery");
        recoveryCase.ChangeStatus(RecoveryCaseStatus.Resolved, "op-002", "Recovery complete");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => recoveryCase.RecordAction("EndSession", "op-003", "Retry"));

        // Assert
        Assert.Equal("Recovery action cannot be recorded on a terminal recovery case.", exception.Message);
        Assert.Equal(3, recoveryCase.Events.Count);
    }

    [Fact]
    public void RecordIdempotentAudit_AllowsResolvedRecoveryCaseAudit()
    {
        // Arrange
        var recoveryCase = new RecoveryCase(Guid.NewGuid(), Guid.NewGuid(), "op-001", "Initial");
        recoveryCase.ChangeStatus(RecoveryCaseStatus.InProgress, "op-002", "Starting recovery");
        recoveryCase.ChangeStatus(RecoveryCaseStatus.Resolved, "op-002", "Recovery complete");

        // Act
        recoveryCase.RecordIdempotentAudit("EndSessionAlreadyEnded", "op-003", "Retry");

        // Assert
        Assert.Equal(4, recoveryCase.Events.Count);
        var actionEvent = Assert.IsType<RecoveryActionRecorded>(recoveryCase.Events[3]);
        Assert.Equal("EndSessionAlreadyEnded", actionEvent.ActionName);
        Assert.Equal("op-003", actionEvent.OperatorId);
        Assert.Equal("Retry", actionEvent.Reason);
    }

    [Fact]
    public void RecordIdempotentAudit_RejectsNewRecoveryCase()
    {
        // Arrange
        var recoveryCase = new RecoveryCase(Guid.NewGuid(), Guid.NewGuid(), "op-001", "Initial");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => recoveryCase.RecordIdempotentAudit("EndSessionAlreadyEnded", "op-002", "Retry"));

        // Assert
        Assert.Equal("Idempotent audit can only be recorded after recovery work has started.", exception.Message);
        Assert.Single(recoveryCase.Events);
    }

    [Fact]
    public void RecordIdempotentAudit_RejectsAbandonedRecoveryCase()
    {
        // Arrange
        var recoveryCase = new RecoveryCase(Guid.NewGuid(), Guid.NewGuid(), "op-001", "Initial");
        recoveryCase.ChangeStatus(RecoveryCaseStatus.Abandoned, "op-002", "No longer needed");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => recoveryCase.RecordIdempotentAudit("EndSessionAlreadyEnded", "op-003", "Retry"));

        // Assert
        Assert.Equal("Idempotent audit cannot be recorded on an abandoned recovery case.", exception.Message);
        Assert.Equal(2, recoveryCase.Events.Count);
    }

    [Fact]
    public void MultipleActions_PreservesAuditTrail()
    {
        // Arrange
        var recoveryCase = new RecoveryCase(Guid.NewGuid(), Guid.NewGuid(), "op-001", "Initial");

        // Act
        recoveryCase.ChangeStatus(RecoveryCaseStatus.InProgress, "op-002", "Starting recovery");
        recoveryCase.RecordAction("EndSession", "op-002", "End stuck session");
        recoveryCase.ChangeStatus(RecoveryCaseStatus.Resolved, "op-002", "Recovery complete");

        // Assert
        Assert.Equal(4, recoveryCase.Events.Count);
        Assert.All(recoveryCase.Events, @event => Assert.NotNull(@event));
    }
}
