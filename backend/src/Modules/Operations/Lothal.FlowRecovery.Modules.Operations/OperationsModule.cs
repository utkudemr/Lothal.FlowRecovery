namespace Lothal.FlowRecovery.Modules.Operations;

using Lothal.FlowRecovery.Modules.Session;

/// <summary>
/// Operations module coordinates operator-driven recovery workflows.
/// Manages recovery cases, audit trails, and manual recovery actions.
/// </summary>
public class OperationsModule
{
    private readonly SessionModule _sessionModule;

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
}

/// <summary>
/// Represents a session that is a candidate for operator-driven recovery.
/// </summary>
public sealed record RecoveryCandidateSnapshot(
    Guid SessionId,
    string FlowId,
    string CurrentStep,
    DateTime LastEventAtUtc);

