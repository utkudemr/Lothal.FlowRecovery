namespace Lothal.FlowRecovery.Modules.Operations.Domain;

/// <summary>
/// Represents the status of a recovery case.
/// </summary>
public enum RecoveryCaseStatus
{
    New,
    InProgress,
    Resolved,
    Abandoned
}

/// <summary>
/// Represents a recovery case for a stale session.
/// Recovery cases track operator-initiated recovery workflows.
/// </summary>
public class RecoveryCase
{
    private readonly List<IRecoveryCaseEvent> _events = new();

    public Guid Id { get; }
    public Guid SessionId { get; }
    public DateTime CreatedAtUtc { get; }
    public string CreatedByOperatorId { get; }
    public RecoveryCaseStatus Status { get; private set; }
    public IReadOnlyList<IRecoveryCaseEvent> Events => _events.AsReadOnly();

    public RecoveryCase(Guid id, Guid sessionId, string createdByOperatorId, string reason)
    {
        Id = id;
        SessionId = sessionId;
        CreatedByOperatorId = createdByOperatorId;
        CreatedAtUtc = DateTime.UtcNow;
        Status = RecoveryCaseStatus.New;

        var @event = new RecoveryCaseOpened(id, sessionId, createdByOperatorId, reason, CreatedAtUtc);
        _events.Add(@event);
    }

    private RecoveryCase()
    {
        Id = Guid.Empty;
        SessionId = Guid.Empty;
        CreatedByOperatorId = string.Empty;
        CreatedAtUtc = DateTime.UtcNow;
        Status = RecoveryCaseStatus.New;
    }

    public void ChangeStatus(RecoveryCaseStatus newStatus, string operatorId, string reason)
    {
        if (Status == newStatus)
            return;

        Status = newStatus;
        var @event = new RecoveryCaseStatusChanged(Id, Status, operatorId, reason, DateTime.UtcNow);
        _events.Add(@event);
    }

    public void RecordAction(string actionName, string operatorId, string reason)
    {
        var @event = new RecoveryActionRecorded(Id, actionName, operatorId, reason, DateTime.UtcNow);
        _events.Add(@event);
    }
}

/// <summary>
/// Base interface for recovery case events.
/// All events are immutable and include operator audit metadata.
/// </summary>
public interface IRecoveryCaseEvent
{
    Guid RecoveryCaseId { get; }
    string OperatorId { get; }
    string Reason { get; }
    DateTime TimestampUtc { get; }
}

/// <summary>
/// Emitted when a recovery case is opened for a stale session.
/// </summary>
public record RecoveryCaseOpened(
    Guid RecoveryCaseId,
    Guid SessionId,
    string OperatorId,
    string Reason,
    DateTime TimestampUtc) : IRecoveryCaseEvent;

/// <summary>
/// Emitted when a recovery case status changes.
/// </summary>
public record RecoveryCaseStatusChanged(
    Guid RecoveryCaseId,
    RecoveryCaseStatus NewStatus,
    string OperatorId,
    string Reason,
    DateTime TimestampUtc) : IRecoveryCaseEvent;

/// <summary>
/// Emitted when a recovery action (e.g., EndSession) is executed.
/// Captures the action name and operator metadata for audit trail.
/// </summary>
public record RecoveryActionRecorded(
    Guid RecoveryCaseId,
    string ActionName,
    string OperatorId,
    string Reason,
    DateTime TimestampUtc) : IRecoveryCaseEvent;
