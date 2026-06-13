namespace Lothal.FlowRecovery.Modules.Operations.Domain;

using System.Collections.Concurrent;

/// <summary>
/// In-memory store for recovery cases.
/// </summary>
public class InMemoryRecoveryCaseStore
{
    private readonly ConcurrentDictionary<Guid, RecoveryCase> _casesByRecoveryId = new();
    private readonly ConcurrentDictionary<Guid, Guid> _recoveryIdBySessionId = new();

    public bool TryGetBySessionId(Guid sessionId, out RecoveryCase? recoveryCase)
    {
        recoveryCase = null;
        if (_recoveryIdBySessionId.TryGetValue(sessionId, out var caseId))
        {
            return _casesByRecoveryId.TryGetValue(caseId, out recoveryCase);
        }
        return false;
    }

    public bool TryGet(Guid recoveryId, out RecoveryCase? recoveryCase)
    {
        return _casesByRecoveryId.TryGetValue(recoveryId, out recoveryCase);
    }

    public void Save(RecoveryCase recoveryCase)
    {
        _casesByRecoveryId[recoveryCase.Id] = recoveryCase;
        _recoveryIdBySessionId[recoveryCase.SessionId] = recoveryCase.Id;
    }
}
