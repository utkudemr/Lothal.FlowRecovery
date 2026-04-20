namespace Lothal.FlowRecovery.Modules.Session;

public sealed class SessionRecord
{
    private readonly List<SessionEvent> _events;

    public Guid SessionId { get; }
    public string FlowId { get; }
    public string StartedBy { get; }
    public string Status { get; private set; }
    public string? CurrentStep { get; private set; }
    public DateTime StartedAtUtc { get; }
    public DateTime? EndedAtUtc { get; private set; }
    public IReadOnlyList<SessionEvent> Events => _events;

    private SessionRecord(
        Guid sessionId,
        string flowId,
        string startedBy,
        string status,
        string? currentStep,
        DateTime startedAtUtc,
        DateTime? endedAtUtc,
        List<SessionEvent> events)
    {
        SessionId = sessionId;
        FlowId = flowId;
        StartedBy = startedBy;
        Status = status;
        CurrentStep = currentStep;
        StartedAtUtc = startedAtUtc;
        EndedAtUtc = endedAtUtc;
        _events = events;
    }

    public static SessionRecord Create(
        string flowId,
        string startedBy,
        DateTime startedAtUtc,
        SessionStartedEvent startedEvent)
    {
        return new SessionRecord(
            Guid.NewGuid(),
            flowId,
            startedBy,
            "Active",
            null,
            startedAtUtc,
            null,
            new List<SessionEvent> { startedEvent });
    }

    public bool SetCurrentStep(string currentStep, string changedBy, string actorType, string? reason, DateTime occurredAtUtc)
    {
        if (Status != "Active")
        {
            return false;
        }

        var normalizedCurrentStep = currentStep?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedCurrentStep))
        {
            throw new ArgumentException("Current step is required.", nameof(currentStep));
        }

        if (occurredAtUtc == default)
        {
            throw new ArgumentException("Current step timestamp is required.", nameof(occurredAtUtc));
        }

        if (occurredAtUtc < StartedAtUtc)
        {
            throw new ArgumentException("Current step timestamp cannot be earlier than the session start timestamp.", nameof(occurredAtUtc));
        }

        var normalizedChangedBy = changedBy?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedChangedBy))
        {
            throw new ArgumentException("ChangedBy is required.", nameof(changedBy));
        }

        var trimmedActorType = actorType?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedActorType))
        {
            throw new ArgumentException("ActorType is required.", nameof(actorType));
        }

        var normalizedActorType = NormalizeActorType(trimmedActorType);
        if (normalizedActorType is null)
        {
            throw new ArgumentException("ActorType is invalid.", nameof(actorType));
        }

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason))
        {
            normalizedReason = null;
        }

        if (CurrentStep == normalizedCurrentStep)
        {
            return false;
        }

        var previousStep = CurrentStep;
        CurrentStep = normalizedCurrentStep;
        _events.Add(new SessionCurrentStepSetEvent(
            SessionId,
            FlowId,
            normalizedChangedBy,
            normalizedActorType,
            normalizedReason,
            previousStep,
            CurrentStep,
            occurredAtUtc));
        return true;
    }

    public bool End(SessionEndMetadata endMetadata, DateTime endedAtUtc)
    {
        if (Status == "Ended")
        {
            return false;
        }

        ArgumentNullException.ThrowIfNull(endMetadata);

        if (endedAtUtc == default)
        {
            throw new ArgumentException("End timestamp is required.", nameof(endedAtUtc));
        }

        if (endedAtUtc < StartedAtUtc)
        {
            throw new ArgumentException("End timestamp cannot be earlier than the session start timestamp.", nameof(endedAtUtc));
        }

        if (Status != "Active")
        {
            return false;
        }

        var previousStatus = Status;
        Status = "Ended";
        EndedAtUtc = endedAtUtc;
        _events.Add(new SessionEndedEvent(
            SessionId,
            FlowId,
            endMetadata.EndedBy,
            endMetadata.ActorType,
            endMetadata.Reason,
            previousStatus,
            Status,
            endedAtUtc));
        return true;
    }

    public void RecordAlreadyEndedAudit(SessionEndMetadata endMetadata, DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(endMetadata);

        if (Status != "Ended")
        {
            throw new InvalidOperationException("Already-ended audit can only be recorded for ended sessions.");
        }

        if (occurredAtUtc == default)
        {
            throw new ArgumentException("Audit timestamp is required.", nameof(occurredAtUtc));
        }

        if (occurredAtUtc < StartedAtUtc)
        {
            throw new ArgumentException("Audit timestamp cannot be earlier than the session start timestamp.", nameof(occurredAtUtc));
        }

        if (EndedAtUtc.HasValue && occurredAtUtc < EndedAtUtc.Value)
        {
            throw new ArgumentException("Audit timestamp cannot be earlier than the session end timestamp.", nameof(occurredAtUtc));
        }

        _events.Add(new SessionEndAlreadyEndedAuditEvent(
            SessionId,
            FlowId,
            endMetadata.EndedBy,
            endMetadata.ActorType,
            endMetadata.Reason,
            Status,
            EndedAtUtc,
            occurredAtUtc));
    }

    private static string? NormalizeActorType(string actorType)
    {
        if (string.Equals(actorType, "Operator", StringComparison.OrdinalIgnoreCase))
        {
            return "Operator";
        }

        if (string.Equals(actorType, "System", StringComparison.OrdinalIgnoreCase))
        {
            return "System";
        }

        return null;
    }
}

public abstract record SessionEvent(DateTime OccurredAtUtc);

public sealed record SessionStartedEvent(
    string FlowId,
    string StartedBy,
    DateTime OccurredAtUtc) : SessionEvent(OccurredAtUtc);

public sealed record SessionCurrentStepSetEvent(
    Guid SessionId,
    string FlowId,
    string ChangedBy,
    string ActorType,
    string? Reason,
    string? PreviousStep,
    string CurrentStep,
    DateTime OccurredAtUtc) : SessionEvent(OccurredAtUtc);

public sealed record SessionEndedEvent(
    Guid SessionId,
    string FlowId,
    string EndedBy,
    string ActorType,
    string? Reason,
    string PreviousStatus,
    string NewStatus,
    DateTime OccurredAtUtc) : SessionEvent(OccurredAtUtc);

public sealed record SessionEndAlreadyEndedAuditEvent(
    Guid SessionId,
    string FlowId,
    string EndedBy,
    string ActorType,
    string? Reason,
    string CurrentStatus,
    DateTime? ExistingEndedAtUtc,
    DateTime OccurredAtUtc) : SessionEvent(OccurredAtUtc);
