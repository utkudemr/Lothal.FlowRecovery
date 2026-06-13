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
        if (!TryResolveStaleBeforeUtc(staleBeforeUtc, staleForMinutes, out var resolvedStaleBeforeUtc, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["staleBeforeUtc"] = new[] { error! },
            });
        }

        var response = operationsModule
            .GetRecoveryCandidates(resolvedStaleBeforeUtc)
            .Select(candidate => candidate.ToResponse());

        return Results.Ok(response);
    });

app.Run();

return;

static bool TryResolveStaleBeforeUtc(
    DateTime? staleBeforeUtc,
    double? staleForMinutes,
    out DateTime resolvedStaleBeforeUtc,
    out string? error)
{
    var hasStaleBeforeUtc = staleBeforeUtc.HasValue;
    var hasStaleForMinutes = staleForMinutes.HasValue;

    if (hasStaleBeforeUtc == hasStaleForMinutes)
    {
        resolvedStaleBeforeUtc = default;
        error = "Provide exactly one of staleBeforeUtc or staleForMinutes.";
        return false;
    }

    if (hasStaleForMinutes)
    {
        var staleForMinutesValue = staleForMinutes.GetValueOrDefault();

        if (staleForMinutesValue <= 0)
        {
            resolvedStaleBeforeUtc = default;
            error = "staleForMinutes must be greater than zero.";
            return false;
        }

        resolvedStaleBeforeUtc = DateTime.UtcNow.AddMinutes(-staleForMinutesValue);
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
