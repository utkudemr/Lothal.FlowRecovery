namespace Lothal.FlowRecovery.Modules.Operations.Tests;

using Lothal.FlowRecovery.Modules.Operations.Domain;

public class OperationsApiContractsTests
{
    [Fact]
    public void RecoveryCandidateSnapshot_ToResponse_MapsPlainContract()
    {
        var candidate = new RecoveryCandidateSnapshot(
            Guid.NewGuid(),
            "flow-123",
            "checkout",
            new DateTime(2026, 6, 13, 10, 30, 0, DateTimeKind.Utc));

        var response = candidate.ToResponse();

        Assert.Equal(candidate.SessionId, response.SessionId);
        Assert.Equal("flow-123", response.FlowId);
        Assert.Equal("checkout", response.CurrentStep);
        Assert.Equal(candidate.LastEventAtUtc, response.LastEventAtUtc);
    }

    [Fact]
    public void OpenRecoveryCaseResult_ToResponse_MapsRecoveryCaseDetailWithoutExposingDomainType()
    {
        var recoveryCase = new RecoveryCase(Guid.NewGuid(), Guid.NewGuid(), "operator-001", "Initial review");
        var result = new OpenRecoveryCaseResult(true, recoveryCase, null);

        var response = result.ToResponse();

        Assert.True(response.Success);
        Assert.Null(response.Error);
        Assert.NotNull(response.RecoveryCase);
        Assert.Equal(recoveryCase.Id, response.RecoveryCase.RecoveryCaseId);
        Assert.Equal(nameof(RecoveryCaseOpened), response.RecoveryCase.Events[0].EventType);
    }

    [Fact]
    public void RecoveryCase_ToResponse_MapsLifecycleAndActionEventsToApiContracts()
    {
        var recoveryCase = new RecoveryCase(Guid.NewGuid(), Guid.NewGuid(), "operator-001", "Initial");
        recoveryCase.ChangeStatus(RecoveryCaseStatus.InProgress, "operator-002", "Start recovery");
        recoveryCase.RecordAction("EndSession", "operator-002", "End stuck session");
        recoveryCase.ChangeStatus(RecoveryCaseStatus.Resolved, "operator-002", "Recovery complete");

        var response = recoveryCase.ToResponse();

        Assert.Equal(recoveryCase.Id, response.RecoveryCaseId);
        Assert.Equal(recoveryCase.SessionId, response.SessionId);
        Assert.Equal("Resolved", response.Status);
        Assert.Equal(4, response.Events.Count);
        Assert.Equal(nameof(RecoveryCaseOpened), response.Events[0].EventType);
        Assert.Equal(nameof(RecoveryCaseStatusChanged), response.Events[1].EventType);
        Assert.Equal("InProgress", response.Events[1].NewStatus);
        Assert.Equal(nameof(RecoveryActionRecorded), response.Events[2].EventType);
        Assert.Equal("EndSession", response.Events[2].ActionName);
        Assert.Equal(nameof(RecoveryCaseStatusChanged), response.Events[3].EventType);
        Assert.Equal("Resolved", response.Events[3].NewStatus);
    }

    [Fact]
    public void ManualEndSessionRecoveryResult_ToResponse_MapsSuccessAndError()
    {
        var success = new ManualEndSessionRecoveryResult(true, null);
        var failure = new ManualEndSessionRecoveryResult(false, "Recovery case not found");

        var successResponse = success.ToResponse();
        var failureResponse = failure.ToResponse();

        Assert.True(successResponse.Success);
        Assert.Null(successResponse.Error);
        Assert.Null(successResponse.Outcome);
        Assert.False(failureResponse.Success);
        Assert.Equal("Recovery case not found", failureResponse.Error);
        Assert.Null(failureResponse.Outcome);
    }
}
