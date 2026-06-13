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
    public RecoveryCase OpenRecoveryCase(Guid sessionId, string operatorId, string reason)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("SessionId is required.", nameof(sessionId));
        }

        if (string.IsNullOrWhiteSpace(operatorId))
        {
            throw new ArgumentException("OperatorId is required.", nameof(operatorId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason is required.", nameof(reason));
        }

        // Check if a case already exists for this session (idempotent)
        if (_recoveryStore.TryGetBySessionId(sessionId, out var existingCase))
        {
            return existingCase!;
        }

        // Create new recovery case
        var recoveryCase = new RecoveryCase(Guid.NewGuid(), sessionId, operatorId, reason);
        _recoveryStore.Save(recoveryCase);
        return recoveryCase;
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

        // End the session via Session module with operator metadata
        var endResult = _sessionModule.EndSession(new EndSessionCommand(recoveryCase.SessionId, operatorId, "Operator", reason));
        if (!endResult.Success)
        {
            return new ManualEndSessionRecoveryResult(false, endResult.Error ?? "Failed to end session");
        }

        // Record the action in the recovery case
        recoveryCase.RecordAction("EndSession", operatorId, reason);
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
/// Represents a session that is a candidate for operator-driven recovery.
/// </summary>
public sealed record RecoveryCandidateSnapshot(
    Guid SessionId,
    string FlowId,
    string CurrentStep,
    DateTime LastEventAtUtc);

