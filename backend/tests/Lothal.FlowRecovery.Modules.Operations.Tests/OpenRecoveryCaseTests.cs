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

        // Act
        var recoveryCase = operationsModule.OpenRecoveryCase(actualSessionId, operatorId, reason);

        // Assert
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

        // Act - Open case twice
        var firstCase = operationsModule.OpenRecoveryCase(sessionId, operatorId, reason);
        var secondCase = operationsModule.OpenRecoveryCase(sessionId, "operator-002", "Different reason");

        // Assert - Same case should be returned both times
        Assert.Equal(firstCase.Id, secondCase.Id);
        Assert.Single(firstCase.Events); // No new events added on second call
    }

    [Theory]
    [InlineData("operator-001", "Stale session detected", "sessionId", "SessionId is required.")]
    [InlineData(null, "Stale session detected", "operatorId", "OperatorId is required.")]
    [InlineData(" ", "Stale session detected", "operatorId", "OperatorId is required.")]
    [InlineData("operator-001", null, "reason", "Reason is required.")]
    [InlineData("operator-001", " ", "reason", "Reason is required.")]
    public void OpenRecoveryCase_RejectsInvalidBoundaryInputs(
        string? operatorId,
        string? reason,
        string expectedParamName,
        string expectedMessage)
    {
        // Arrange
        var sessionModule = new SessionModule();
        var operationsModule = new OperationsModule(sessionModule);
        var sessionId = expectedParamName == "sessionId" ? Guid.Empty : Guid.NewGuid();

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => operationsModule.OpenRecoveryCase(sessionId, operatorId!, reason!));

        // Assert
        Assert.Equal(expectedParamName, exception.ParamName);
        Assert.Contains(expectedMessage, exception.Message);
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
        var recoveryCase = operationsModule.OpenRecoveryCase(sessionId, "operator-001", "Initial");

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
        var recoveryCase = operationsModule.OpenRecoveryCase(sessionId, "operator-001", "Initial");

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
