namespace Lothal.FlowRecovery.Modules.Operations;

using Lothal.FlowRecovery.Modules.Operations.Domain;

/// <summary>
/// Request contract for listing stale active recovery candidates.
/// Validation expectation: provide exactly one stale boundary via <see cref="StaleBeforeUtc"/>
/// or <see cref="StaleFor"/>, and use a positive duration when <see cref="StaleFor"/> is supplied.
/// </summary>
public sealed record ListRecoveryCandidatesRequest(
    DateTime? StaleBeforeUtc,
    TimeSpan? StaleFor);

/// <summary>
/// Response contract for a stale active session that may require operator intervention.
/// </summary>
public sealed record RecoveryCandidateResponse(
    Guid SessionId,
    string FlowId,
    string CurrentStep,
    DateTime LastEventAtUtc);

/// <summary>
/// Request contract for opening a recovery case.
/// Validation expectation: <see cref="SessionId"/> must be non-empty, <see cref="OperatorId"/>
/// and <see cref="Reason"/> must be non-blank, and callers should provide exactly one stale
/// boundary via <see cref="StaleBeforeUtc"/> or <see cref="StaleFor"/>.
/// </summary>
public sealed record OpenRecoveryCaseRequest(
    Guid SessionId,
    string OperatorId,
    string Reason,
    DateTime? StaleBeforeUtc,
    TimeSpan? StaleFor);

/// <summary>
/// Response contract for opening or retrieving a recovery case.
/// </summary>
public sealed record OpenRecoveryCaseResponse(
    bool Success,
    RecoveryCaseDetailResponse? RecoveryCase,
    string? Error);

/// <summary>
/// Request contract for manual end-session recovery.
/// Validation expectation: <see cref="RecoveryCaseId"/> must be non-empty and
/// <see cref="OperatorId"/> and <see cref="Reason"/> must be non-blank.
/// </summary>
public sealed record ManualEndSessionRecoveryRequest(
    Guid RecoveryCaseId,
    string OperatorId,
    string Reason);

/// <summary>
/// Response contract for manual end-session recovery.
/// </summary>
public sealed record ManualEndSessionRecoveryResponse(
    bool Success,
    string? Error);

/// <summary>
/// Request contract for retrieving a recovery case detail projection.
/// Validation expectation: <see cref="RecoveryCaseId"/> must be non-empty.
/// </summary>
public sealed record GetRecoveryCaseDetailRequest(
    Guid RecoveryCaseId);

/// <summary>
/// Response contract for a recovery case detail view.
/// </summary>
public sealed record RecoveryCaseDetailResponse(
    Guid RecoveryCaseId,
    Guid SessionId,
    DateTime CreatedAtUtc,
    string CreatedByOperatorId,
    string Status,
    IReadOnlyList<RecoveryCaseEventResponse> Events);

/// <summary>
/// Response contract for a recovery case event.
/// </summary>
public sealed record RecoveryCaseEventResponse(
    string EventType,
    string OperatorId,
    string Reason,
    DateTime TimestampUtc,
    string? ActionName,
    string? NewStatus,
    Guid? SessionId);

/// <summary>
/// Maps Operations domain and module results to API-safe response contracts.
/// </summary>
public static class OperationsApiContractMapper
{
    public static RecoveryCandidateResponse ToResponse(this RecoveryCandidateSnapshot candidate)
    {
        return new RecoveryCandidateResponse(
            candidate.SessionId,
            candidate.FlowId,
            candidate.CurrentStep,
            candidate.LastEventAtUtc);
    }

    public static OpenRecoveryCaseResponse ToResponse(this OpenRecoveryCaseResult result)
    {
        return new OpenRecoveryCaseResponse(
            result.Success,
            result.RecoveryCase?.ToResponse(),
            result.Error);
    }

    public static ManualEndSessionRecoveryResponse ToResponse(this ManualEndSessionRecoveryResult result)
    {
        return new ManualEndSessionRecoveryResponse(
            result.Success,
            result.Error);
    }

    public static RecoveryCaseDetailResponse ToResponse(this RecoveryCase recoveryCase)
    {
        return new RecoveryCaseDetailResponse(
            recoveryCase.Id,
            recoveryCase.SessionId,
            recoveryCase.CreatedAtUtc,
            recoveryCase.CreatedByOperatorId,
            recoveryCase.Status.ToString(),
            recoveryCase.Events.Select(ToResponse).ToList().AsReadOnly());
    }

    private static RecoveryCaseEventResponse ToResponse(IRecoveryCaseEvent @event)
    {
        return @event switch
        {
            RecoveryCaseOpened opened => new RecoveryCaseEventResponse(
                nameof(RecoveryCaseOpened),
                opened.OperatorId,
                opened.Reason,
                opened.TimestampUtc,
                null,
                null,
                opened.SessionId),
            RecoveryCaseStatusChanged statusChanged => new RecoveryCaseEventResponse(
                nameof(RecoveryCaseStatusChanged),
                statusChanged.OperatorId,
                statusChanged.Reason,
                statusChanged.TimestampUtc,
                null,
                statusChanged.NewStatus.ToString(),
                null),
            RecoveryActionRecorded actionRecorded => new RecoveryCaseEventResponse(
                nameof(RecoveryActionRecorded),
                actionRecorded.OperatorId,
                actionRecorded.Reason,
                actionRecorded.TimestampUtc,
                actionRecorded.ActionName,
                null,
                null),
            _ => throw new InvalidOperationException($"Unsupported recovery case event type {@event.GetType().Name}."),
        };
    }
}
