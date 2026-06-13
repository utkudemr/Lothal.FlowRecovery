namespace Lothal.FlowRecovery.Modules.Operations.Tests;

using Lothal.FlowRecovery.Modules.Operations.Domain;
using Lothal.FlowRecovery.Modules.Session;

public class OpenRecoveryCaseTests
{
    [Fact]
    public void OpenRecoveryCase_CreatesNewCase()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);
        var sessionId = Guid.NewGuid();
        var operatorId = "operator-001";
        var reason = "Stale session detected";

        // Create a session
        var sessionResult = sessionModule.StartSession(new StartSessionCommand("flow-test-" + sessionId, "user-001"));
        var actualSessionId = sessionResult.SessionId!.Value;
        var staleBeforeUtc = DateTime.UtcNow.AddSeconds(1);

        // Act
        var result = operationsModule.OpenRecoveryCase(actualSessionId, staleBeforeUtc, operatorId, reason);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Error);
        var recoveryCase = Assert.IsType<RecoveryCase>(result.RecoveryCase);
        Assert.NotEqual(Guid.Empty, recoveryCase.Id);
        Assert.Equal(actualSessionId, recoveryCase.SessionId);
        Assert.Equal(operatorId, recoveryCase.CreatedByOperatorId);
        Assert.Equal(RecoveryCaseStatus.New, recoveryCase.Status);
        Assert.Single(recoveryCase.Events);
    }

    [Fact]
    public void OpenRecoveryCase_IsIdempotent()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);
        var operatorId = "operator-001";
        var reason = "Stale session detected";

        // Create a session
        var sessionResult = sessionModule.StartSession(new StartSessionCommand("flow-idempotent-" + Guid.NewGuid(), "user-001"));
        var sessionId = sessionResult.SessionId!.Value;
        var staleBeforeUtc = DateTime.UtcNow.AddSeconds(1);

        // Act - Open case twice
        var firstResult = operationsModule.OpenRecoveryCase(sessionId, staleBeforeUtc, operatorId, reason);
        var secondResult = operationsModule.OpenRecoveryCase(sessionId, staleBeforeUtc, "operator-002", "Different reason");

        // Assert - Same case should be returned both times
        Assert.True(firstResult.Success);
        Assert.True(secondResult.Success);
        var firstCase = Assert.IsType<RecoveryCase>(firstResult.RecoveryCase);
        var secondCase = Assert.IsType<RecoveryCase>(secondResult.RecoveryCase);
        Assert.Equal(firstCase.Id, secondCase.Id);
        Assert.Equal(2, firstCase.Events.Count);
        var duplicateEvent = Assert.IsType<RecoveryActionRecorded>(firstCase.Events[1]);
        Assert.Equal("OpenRecoveryCaseDuplicate", duplicateEvent.ActionName);
        Assert.Equal("operator-002", duplicateEvent.OperatorId);
        Assert.Equal("Different reason", duplicateEvent.Reason);
    }

    [Theory]
    [InlineData("operator-001", "Stale session detected", true, "SessionId is required.")]
    [InlineData(null, "Stale session detected", false, "OperatorId is required.")]
    [InlineData(" ", "Stale session detected", false, "OperatorId is required.")]
    [InlineData("operator-001", null, false, "Reason is required.")]
    [InlineData("operator-001", " ", false, "Reason is required.")]
    public void OpenRecoveryCase_RejectsInvalidBoundaryInputs(
        string? operatorId,
        string? reason,
        bool useEmptySessionId,
        string expectedMessage)
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);
        var sessionId = useEmptySessionId ? Guid.Empty : Guid.NewGuid();

        // Act
        var result = operationsModule.OpenRecoveryCase(sessionId, DateTime.UtcNow, operatorId!, reason!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(expectedMessage, result.Error);
        Assert.Null(result.RecoveryCase);
    }

    [Fact]
    public void OpenRecoveryCase_RejectsMissingSession()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);

        // Act
        var result = operationsModule.OpenRecoveryCase(Guid.NewGuid(), DateTime.UtcNow, "operator-001", "Stale session detected");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Session not found.", result.Error);
        Assert.Null(result.RecoveryCase);
    }

    [Fact]
    public void OpenRecoveryCase_RejectsNonStaleActiveSession()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);
        var sessionResult = sessionModule.StartSession(new StartSessionCommand("flow-not-stale-" + Guid.NewGuid(), "user-001"));
        var sessionId = sessionResult.SessionId!.Value;
        var staleBeforeUtc = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var result = operationsModule.OpenRecoveryCase(sessionId, staleBeforeUtc, "operator-001", "Stale session detected");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Recovery case can only be opened for a stale active session.", result.Error);
        Assert.Null(result.RecoveryCase);
        Assert.Null(operationsModule.GetRecoveryCaseBySessionId(sessionId));
    }

    [Fact]
    public void OpenRecoveryCase_RejectsEndedSession()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);
        var sessionResult = sessionModule.StartSession(new StartSessionCommand("flow-ended-" + Guid.NewGuid(), "user-001"));
        var sessionId = sessionResult.SessionId!.Value;
        sessionModule.EndSession(new EndSessionCommand(sessionId, "operator-001", "Operator", "Completed"));

        // Act
        var result = operationsModule.OpenRecoveryCase(sessionId, DateTime.UtcNow.AddSeconds(1), "operator-001", "Stale session detected");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Recovery case can only be opened for an active session.", result.Error);
        Assert.Null(result.RecoveryCase);
        Assert.Null(operationsModule.GetRecoveryCaseBySessionId(sessionId));
    }

    [Fact]
    public void GetRecoveryCase_RetrievesCase()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);

        // Create a session and recovery case
        var sessionResult = sessionModule.StartSession(new StartSessionCommand("flow-retrieve-" + Guid.NewGuid(), "user-001"));
        var sessionId = sessionResult.SessionId!.Value;
        var result = operationsModule.OpenRecoveryCase(sessionId, DateTime.UtcNow.AddSeconds(1), "operator-001", "Initial");
        var recoveryCase = Assert.IsType<RecoveryCase>(result.RecoveryCase);

        // Act
        var retrieved = operationsModule.GetRecoveryCase(recoveryCase.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(recoveryCase.Id, retrieved.Id);
        Assert.Equal(sessionId, retrieved.SessionId);
    }

    [Fact]
    public void GetRecoveryCaseBySessionId_RetrievesCase()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);

        // Create a session and recovery case
        var sessionResult = sessionModule.StartSession(new StartSessionCommand("flow-by-session-" + Guid.NewGuid(), "user-001"));
        var sessionId = sessionResult.SessionId!.Value;
        var result = operationsModule.OpenRecoveryCase(sessionId, DateTime.UtcNow.AddSeconds(1), "operator-001", "Initial");
        var recoveryCase = Assert.IsType<RecoveryCase>(result.RecoveryCase);

        // Act
        var retrieved = operationsModule.GetRecoveryCaseBySessionId(sessionId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(recoveryCase.Id, retrieved.Id);
        Assert.Equal(sessionId, retrieved.SessionId);
    }

    [Fact]
    public void GetRecoveryCase_ReturnsNullForMissingCase()
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);

        // Act
        var retrieved = operationsModule.GetRecoveryCase(Guid.NewGuid());

        // Assert
        Assert.Null(retrieved);
    }
}
