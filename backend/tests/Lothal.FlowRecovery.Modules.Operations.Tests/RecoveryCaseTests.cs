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
