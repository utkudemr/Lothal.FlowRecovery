namespace Lothal.FlowRecovery.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using Lothal.FlowRecovery.Modules.Operations;
using Lothal.FlowRecovery.Modules.Session;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

public class RecoveryCandidatesEndpointTests
{
    [Fact]
    public async Task GetRecoveryCandidates_ReturnsStaleCandidatesAndExcludesNonStaleSessions()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var sessionModule = factory.Services.GetRequiredService<SessionModule>();

        var staleSessionId = StartSession(sessionModule, "flow-stale-api-" + Guid.NewGuid().ToString("N"));
        var threshold = sessionModule.GetSession(staleSessionId)!.LastEventAtUtc;

        Assert.True(SpinWait.SpinUntil(() => DateTime.UtcNow > threshold, TimeSpan.FromSeconds(1)));
        var nonStaleSessionId = StartSession(sessionModule, "flow-non-stale-api-" + Guid.NewGuid().ToString("N"));

        var response = await client.GetAsync($"/operations/recovery-candidates?staleBeforeUtc={Uri.EscapeDataString(threshold.ToString("O"))}");

        response.EnsureSuccessStatusCode();
        var candidates = await response.Content.ReadFromJsonAsync<List<RecoveryCandidateResponse>>();

        Assert.NotNull(candidates);
        Assert.Contains(candidates, candidate => candidate.SessionId == staleSessionId);
        Assert.DoesNotContain(candidates, candidate => candidate.SessionId == nonStaleSessionId);
    }

    [Fact]
    public async Task GetRecoveryCandidates_ReturnsBadRequestWhenNoStaleBoundaryIsProvided()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/operations/recovery-candidates");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Guid StartSession(SessionModule sessionModule, string flowId)
    {
        var result = sessionModule.StartSession(new StartSessionCommand(flowId, "user-001"));
        Assert.True(result.Success);
        Assert.NotNull(result.SessionId);
        return result.SessionId.Value;
    }
}
