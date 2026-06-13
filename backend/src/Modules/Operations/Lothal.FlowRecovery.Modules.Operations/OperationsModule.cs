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
}

/// <summary>
/// Represents a session that is a candidate for operator-driven recovery.
/// </summary>
public sealed record RecoveryCandidateSnapshot(
    Guid SessionId,
    string FlowId,
    string CurrentStep,
    DateTime LastEventAtUtc);

