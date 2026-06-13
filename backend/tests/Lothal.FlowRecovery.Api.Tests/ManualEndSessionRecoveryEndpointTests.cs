namespace Lothal.FlowRecovery.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using Lothal.FlowRecovery.Modules.Operations;
using Lothal.FlowRecovery.Modules.Operations.Domain;
using Lothal.FlowRecovery.Modules.Session;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

public class ManualEndSessionRecoveryEndpointTests
{
    [Fact]
    public async Task ManualEndSessionRecovery_ReturnsSuccessfulOutcomeForActiveRecoveryCase()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var services = factory.Services;
        var sessionModule = services.GetRequiredService<SessionModule>();
        var operationsModule = services.GetRequiredService<OperationsModule>();
        var sessionId = StartSession(sessionModule, "flow-manual-end-" + Guid.NewGuid().ToString("N"));
        var openResult = operationsModule.OpenRecoveryCase(sessionId, DateTime.UtcNow.AddSeconds(1), "operator-open", "Initial");
        var recoveryCase = Assert.IsType<RecoveryCase>(openResult.RecoveryCase);

        var request = new ManualEndSessionRecoveryRequest(recoveryCase.Id, "operator-end", "End stuck session");

        var response = await client.PostAsJsonAsync("/operations/recovery-cases/manual-end-session", request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ManualEndSessionRecoveryResponse>();

        Assert.NotNull(body);
        Assert.True(body.Success);
        Assert.Null(body.Error);
        Assert.Equal(nameof(ManualEndSessionRecoveryOutcome.SessionEnded), body.Outcome);
    }

    [Fact]
    public async Task ManualEndSessionRecovery_ReturnsBadRequestForInvalidInput()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var request = new ManualEndSessionRecoveryRequest(Guid.Empty, "operator-end", "End stuck session");

        var response = await client.PostAsJsonAsync("/operations/recovery-cases/manual-end-session", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ManualEndSessionRecoveryResponse>();
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.Equal("RecoveryId is required.", body.Error);
    }

    [Fact]
    public async Task ManualEndSessionRecovery_ReturnsExplicitAlreadyEndedOutcomeForRepeatedAttempt()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var services = factory.Services;
        var sessionModule = services.GetRequiredService<SessionModule>();
        var operationsModule = services.GetRequiredService<OperationsModule>();
        var sessionId = StartSession(sessionModule, "flow-manual-repeat-" + Guid.NewGuid().ToString("N"));
        var openResult = operationsModule.OpenRecoveryCase(sessionId, DateTime.UtcNow.AddSeconds(1), "operator-open", "Initial");
        var recoveryCase = Assert.IsType<RecoveryCase>(openResult.RecoveryCase);

        var firstRequest = new ManualEndSessionRecoveryRequest(recoveryCase.Id, "operator-end", "First end");
        var secondRequest = new ManualEndSessionRecoveryRequest(recoveryCase.Id, "operator-repeat", "Second end");

        var firstResponse = await client.PostAsJsonAsync("/operations/recovery-cases/manual-end-session", firstRequest);
        var secondResponse = await client.PostAsJsonAsync("/operations/recovery-cases/manual-end-session", secondRequest);

        firstResponse.EnsureSuccessStatusCode();
        secondResponse.EnsureSuccessStatusCode();
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<ManualEndSessionRecoveryResponse>();

        Assert.NotNull(secondBody);
        Assert.True(secondBody.Success);
        Assert.Equal(nameof(ManualEndSessionRecoveryOutcome.AlreadyEnded), secondBody.Outcome);

        var session = sessionModule.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.Single(session.Events.OfType<SessionEndedEvent>());
        Assert.Single(session.Events.OfType<SessionEndAlreadyEndedAuditEvent>());
    }

    [Fact]
    public async Task ManualEndSessionRecovery_ReturnsBadRequestForTerminalRecoveryCase()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var services = factory.Services;
        var sessionModule = services.GetRequiredService<SessionModule>();
        var operationsModule = services.GetRequiredService<OperationsModule>();
        var sessionId = StartSession(sessionModule, "flow-manual-terminal-" + Guid.NewGuid().ToString("N"));
        var openResult = operationsModule.OpenRecoveryCase(sessionId, DateTime.UtcNow.AddSeconds(1), "operator-open", "Initial");
        var recoveryCase = Assert.IsType<RecoveryCase>(openResult.RecoveryCase);
        recoveryCase.ChangeStatus(RecoveryCaseStatus.Abandoned, "operator-abandon", "No longer needed");

        var request = new ManualEndSessionRecoveryRequest(recoveryCase.Id, "operator-end", "End stuck session");

        var response = await client.PostAsJsonAsync("/operations/recovery-cases/manual-end-session", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ManualEndSessionRecoveryResponse>();
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.Equal("Recovery case is already terminal.", body.Error);
    }

    private static Guid StartSession(SessionModule sessionModule, string flowId)
    {
        var result = sessionModule.StartSession(new StartSessionCommand(flowId, "user-001"));
        Assert.True(result.Success);
        Assert.NotNull(result.SessionId);
        return result.SessionId.Value;
    }
}
