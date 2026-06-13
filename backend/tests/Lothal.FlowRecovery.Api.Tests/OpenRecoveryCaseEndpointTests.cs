namespace Lothal.FlowRecovery.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using Lothal.FlowRecovery.Modules.Operations;
using Lothal.FlowRecovery.Modules.Session;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

public class OpenRecoveryCaseEndpointTests
{
    [Fact]
    public async Task OpenRecoveryCase_ReturnsRecoveryCaseDetailForStaleActiveSession()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var sessionModule = factory.Services.GetRequiredService<SessionModule>();
        var sessionId = StartSession(sessionModule, "flow-open-case-" + Guid.NewGuid().ToString("N"));

        var request = new OpenRecoveryCaseRequest(
            sessionId,
            "operator-001",
            "Stale session detected",
            DateTime.UtcNow.AddSeconds(1),
            null);

        var response = await client.PostAsJsonAsync("/operations/recovery-cases", request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<OpenRecoveryCaseResponse>();

        Assert.NotNull(body);
        Assert.True(body.Success);
        Assert.Null(body.Error);
        Assert.NotNull(body.RecoveryCase);
        Assert.Equal(sessionId, body.RecoveryCase.SessionId);
        Assert.Equal("New", body.RecoveryCase.Status);
    }

    [Fact]
    public async Task OpenRecoveryCase_ReturnsBadRequestForExpectedBusinessFailure()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var request = new OpenRecoveryCaseRequest(
            Guid.NewGuid(),
            "operator-001",
            "Stale session detected",
            DateTime.UtcNow.AddSeconds(1),
            null);

        var response = await client.PostAsJsonAsync("/operations/recovery-cases", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OpenRecoveryCaseResponse>();
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.Equal("Session not found.", body.Error);
    }

    [Fact]
    public async Task OpenRecoveryCase_ReturnsExplicitDuplicateOpenBehavior()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var sessionModule = factory.Services.GetRequiredService<SessionModule>();
        var sessionId = StartSession(sessionModule, "flow-duplicate-open-" + Guid.NewGuid().ToString("N"));

        var firstRequest = new OpenRecoveryCaseRequest(
            sessionId,
            "operator-001",
            "First open",
            DateTime.UtcNow.AddSeconds(1),
            null);

        var secondRequest = new OpenRecoveryCaseRequest(
            sessionId,
            "operator-002",
            "Second open",
            DateTime.UtcNow.AddSeconds(1),
            null);

        var firstResponse = await client.PostAsJsonAsync("/operations/recovery-cases", firstRequest);
        var secondResponse = await client.PostAsJsonAsync("/operations/recovery-cases", secondRequest);

        firstResponse.EnsureSuccessStatusCode();
        secondResponse.EnsureSuccessStatusCode();

        var firstBody = await firstResponse.Content.ReadFromJsonAsync<OpenRecoveryCaseResponse>();
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<OpenRecoveryCaseResponse>();

        Assert.NotNull(firstBody);
        Assert.NotNull(secondBody);
        Assert.NotNull(firstBody.RecoveryCase);
        Assert.NotNull(secondBody.RecoveryCase);
        Assert.Equal(firstBody.RecoveryCase.RecoveryCaseId, secondBody.RecoveryCase.RecoveryCaseId);
        Assert.Equal(2, secondBody.RecoveryCase.Events.Count);
        Assert.Equal("RecoveryActionRecorded", secondBody.RecoveryCase.Events[1].EventType);
        Assert.Equal("OpenRecoveryCaseDuplicate", secondBody.RecoveryCase.Events[1].ActionName);
    }

    private static Guid StartSession(SessionModule sessionModule, string flowId)
    {
        var result = sessionModule.StartSession(new StartSessionCommand(flowId, "user-001"));
        Assert.True(result.Success);
        Assert.NotNull(result.SessionId);
        return result.SessionId.Value;
    }
}
