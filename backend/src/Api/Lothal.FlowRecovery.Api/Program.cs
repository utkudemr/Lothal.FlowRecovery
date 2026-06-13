using Lothal.FlowRecovery.Modules.Operations;
using Lothal.FlowRecovery.Modules.Session;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SessionModule>();
builder.Services.AddSingleton<OperationsModule>(serviceProvider =>
    new OperationsModule(serviceProvider.GetRequiredService<SessionModule>()));

var app = builder.Build();

app.MapGet(
    "/operations/recovery-candidates",
    (DateTime? staleBeforeUtc, double? staleForMinutes, OperationsModule operationsModule) =>
    {
        var staleFor = staleForMinutes.HasValue
            ? TimeSpan.FromMinutes(staleForMinutes.Value)
            : (TimeSpan?)null;

        if (!TryResolveStaleBeforeUtc(staleBeforeUtc, staleFor, out var resolvedStaleBeforeUtc, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["staleBoundary"] = new[] { error! },
            });
        }

        var response = operationsModule
            .GetRecoveryCandidates(resolvedStaleBeforeUtc)
            .Select(candidate => candidate.ToResponse());

        return Results.Ok(response);
    });

app.MapPost(
    "/operations/recovery-cases",
    (OpenRecoveryCaseRequest request, OperationsModule operationsModule) =>
    {
        if (!TryResolveStaleBeforeUtc(request.StaleBeforeUtc, request.StaleFor, out var resolvedStaleBeforeUtc, out var error))
        {
            return Results.BadRequest(new OpenRecoveryCaseResponse(false, null, error));
        }

        var result = operationsModule.OpenRecoveryCase(
            request.SessionId,
            resolvedStaleBeforeUtc,
            request.OperatorId,
            request.Reason);

        var response = result.ToResponse();
        return result.Success
            ? Results.Ok(response)
            : Results.BadRequest(response);
    });

app.Run();

return;

static bool TryResolveStaleBeforeUtc(
    DateTime? staleBeforeUtc,
    TimeSpan? staleFor,
    out DateTime resolvedStaleBeforeUtc,
    out string? error)
{
    var hasStaleBeforeUtc = staleBeforeUtc.HasValue;
    var hasStaleFor = staleFor.HasValue;

    if (hasStaleBeforeUtc == hasStaleFor)
    {
        resolvedStaleBeforeUtc = default;
        error = "Provide exactly one of staleBeforeUtc or staleFor.";
        return false;
    }

    if (hasStaleFor)
    {
        var staleForValue = staleFor.GetValueOrDefault();

        if (staleForValue <= TimeSpan.Zero)
        {
            resolvedStaleBeforeUtc = default;
            error = "staleFor must be greater than zero.";
            return false;
        }

        resolvedStaleBeforeUtc = DateTime.UtcNow.Subtract(staleForValue);
        error = null;
        return true;
    }

    resolvedStaleBeforeUtc = staleBeforeUtc!.Value;
    error = null;
    return true;
}

public partial class Program
{
}
