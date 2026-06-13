namespace Lothal.FlowRecovery.Modules.Operations;

using Lothal.FlowRecovery.Modules.Operations.Domain;
using Lothal.FlowRecovery.Modules.Session;

/// <summary>
/// Operations module coordinates operator-driven recovery workflows.
/// Manages recovery cases, audit trails, and manual recovery actions.
/// </summary>
public class OperationsModule
{
    private readonly SessionModule _sessionModule;
    private readonly InMemoryRecoveryCaseStore _recoveryStore = new();

    public OperationsModule(SessionModule sessionModule)
    {
        _sessionModule = sessionModule;
    }

    /// <summary>
    /// Lists sessions that are candidates for recovery (stale and active).
    /// </summary>
    public IReadOnlyList<RecoveryCandidateSnapshot> GetRecoveryCandidates(DateTime staleBeforeUtc)
    {
        var staleSessions = _sessionModule.ListStaleActiveSessions(staleBeforeUtc);
        return staleSessions
            .Select(s => new RecoveryCandidateSnapshot(
                s.SessionId,
                s.FlowId,
                s.CurrentStep ?? "unknown",
                s.LastEventAtUtc))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Opens a recovery case for a stale session.
    /// Idempotent: opening the same session twice returns the existing case.
    /// </summary>
    public OpenRecoveryCaseResult OpenRecoveryCase(Guid sessionId, DateTime staleBeforeUtc, string operatorId, string reason)
    {
        if (sessionId == Guid.Empty)
        {
            return new OpenRecoveryCaseResult(false, null, "SessionId is required.");
        }

        if (string.IsNullOrWhiteSpace(operatorId))
        {
            return new OpenRecoveryCaseResult(false, null, "OperatorId is required.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return new OpenRecoveryCaseResult(false, null, "Reason is required.");
        }

        var session = _sessionModule.GetSession(sessionId);
        if (session == null)
        {
            return new OpenRecoveryCaseResult(false, null, "Session not found.");
        }

        if (session.Status != "Active")
        {
            return new OpenRecoveryCaseResult(false, null, "Recovery case can only be opened for an active session.");
        }

        if (session.LastEventAtUtc > staleBeforeUtc)
        {
            return new OpenRecoveryCaseResult(false, null, "Recovery case can only be opened for a stale active session.");
        }

        // Check if a case already exists for this session (idempotent)
        if (_recoveryStore.TryGetBySessionId(sessionId, out var existingCase))
        {
            if (existingCase!.Status is RecoveryCaseStatus.Resolved or RecoveryCaseStatus.Abandoned)
            {
                return new OpenRecoveryCaseResult(false, null, "Recovery case is already terminal.");
            }

            existingCase.RecordAction("OpenRecoveryCaseDuplicate", operatorId, reason);
            _recoveryStore.Save(existingCase);
            return new OpenRecoveryCaseResult(true, existingCase!, null);
        }

        // Create new recovery case
        var recoveryCase = new RecoveryCase(Guid.NewGuid(), sessionId, operatorId, reason);
        _recoveryStore.Save(recoveryCase);
        return new OpenRecoveryCaseResult(true, recoveryCase, null);
    }

    /// <summary>
    /// Gets a recovery case by its ID.
    /// </summary>
    public RecoveryCase? GetRecoveryCase(Guid recoveryId)
    {
        _recoveryStore.TryGet(recoveryId, out var recoveryCase);
        return recoveryCase;
    }

    /// <summary>
    /// Gets a recovery case by session ID.
    /// </summary>
    public RecoveryCase? GetRecoveryCaseBySessionId(Guid sessionId)
    {
        _recoveryStore.TryGetBySessionId(sessionId, out var recoveryCase);
        return recoveryCase;
    }

    /// <summary>
    /// Executes a manual EndSession recovery action.
    /// Ends the session and records the action in the recovery case audit trail.
    /// </summary>
    public ManualEndSessionRecoveryResult ManualEndSessionRecovery(Guid recoveryId, string operatorId, string reason)
    {
        if (recoveryId == Guid.Empty)
        {
            return new ManualEndSessionRecoveryResult(false, "RecoveryId is required.");
        }

        if (string.IsNullOrWhiteSpace(operatorId))
        {
            return new ManualEndSessionRecoveryResult(false, "OperatorId is required.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return new ManualEndSessionRecoveryResult(false, "Reason is required.");
        }

        // Get the recovery case
        var recoveryCase = GetRecoveryCase(recoveryId);
        if (recoveryCase == null)
        {
            return new ManualEndSessionRecoveryResult(false, "Recovery case not found");
        }

        if (recoveryCase.Status == RecoveryCaseStatus.Abandoned)
        {
            return new ManualEndSessionRecoveryResult(false, "Recovery case is already terminal.");
        }

        if (recoveryCase.Status == RecoveryCaseStatus.New)
        {
            recoveryCase.ChangeStatus(RecoveryCaseStatus.InProgress, operatorId, reason);
            _recoveryStore.Save(recoveryCase);
        }

        // End the session via Session module with operator metadata
        var endResult = _sessionModule.EndSession(new EndSessionCommand(recoveryCase.SessionId, operatorId, "Operator", reason));
        if (endResult.Outcome == EndSessionOutcome.AlreadyEnded)
        {
            recoveryCase.RecordIdempotentAudit("EndSessionAlreadyEnded", operatorId, reason);
            if (recoveryCase.Status == RecoveryCaseStatus.InProgress)
            {
                recoveryCase.ChangeStatus(RecoveryCaseStatus.Resolved, operatorId, reason);
            }

            _recoveryStore.Save(recoveryCase);
            return new ManualEndSessionRecoveryResult(true, null);
        }

        if (!endResult.Success)
        {
            return new ManualEndSessionRecoveryResult(false, endResult.Error ?? "Failed to end session");
        }

        // Record the action in the recovery case
        recoveryCase.RecordAction("EndSession", operatorId, reason);
        if (recoveryCase.Status == RecoveryCaseStatus.InProgress)
        {
            recoveryCase.ChangeStatus(RecoveryCaseStatus.Resolved, operatorId, reason);
        }

        _recoveryStore.Save(recoveryCase);

        return new ManualEndSessionRecoveryResult(true, null);
    }
}

/// <summary>
/// Result of a manual EndSession recovery action.
/// </summary>
public sealed record ManualEndSessionRecoveryResult(
    bool Success,
    string? Error);

/// <summary>
/// Result of opening a recovery case.
/// </summary>
public sealed record OpenRecoveryCaseResult(
    bool Success,
    RecoveryCase? RecoveryCase,
    string? Error);

/// <summary>
/// Represents a session that is a candidate for operator-driven recovery.
/// </summary>
public sealed record RecoveryCandidateSnapshot(
    Guid SessionId,
    string FlowId,
    string CurrentStep,
    DateTime LastEventAtUtc);

