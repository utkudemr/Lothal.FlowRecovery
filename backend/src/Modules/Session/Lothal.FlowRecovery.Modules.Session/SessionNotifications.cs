namespace Lothal.FlowRecovery.Modules.Session;

public abstract record SessionNotification(DateTime OccurredAtUtc);

public sealed record StepChangedNotification(
    Guid SessionId,
    string FlowId,
    string CurrentStep,
    string? PreviousStep,
    string ChangedBy,
    string ActorType,
    string? Reason,
    DateTime OccurredAtUtc) : SessionNotification(OccurredAtUtc);

public sealed record SessionStartedNotification(
    Guid SessionId,
    string FlowId,
    string StartedBy,
    DateTime OccurredAtUtc) : SessionNotification(OccurredAtUtc);

public sealed record SessionEndedNotification(
    Guid SessionId,
    string FlowId,
    string EndedBy,
    string ActorType,
    string? Reason,
    string PreviousStatus,
    string NewStatus,
    DateTime OccurredAtUtc) : SessionNotification(OccurredAtUtc);

public static class SessionNotificationMapper
{
    public static SessionNotification? Map(SessionEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return @event switch
        {
            SessionStartedEvent startedEvent => new SessionStartedNotification(
                startedEvent.SessionId,
                startedEvent.FlowId,
                startedEvent.StartedBy,
                startedEvent.OccurredAtUtc),
            SessionCurrentStepSetEvent currentStepSetEvent => new StepChangedNotification(
                currentStepSetEvent.SessionId,
                currentStepSetEvent.FlowId,
                currentStepSetEvent.CurrentStep,
                currentStepSetEvent.PreviousStep,
                currentStepSetEvent.ChangedBy,
                currentStepSetEvent.ActorType,
                currentStepSetEvent.Reason,
                currentStepSetEvent.OccurredAtUtc),
            SessionEndedEvent endedEvent => new SessionEndedNotification(
                endedEvent.SessionId,
                endedEvent.FlowId,
                endedEvent.EndedBy,
                endedEvent.ActorType,
                endedEvent.Reason,
                endedEvent.PreviousStatus,
                endedEvent.NewStatus,
                endedEvent.OccurredAtUtc),
            SessionEndAlreadyEndedAuditEvent
            or SessionCurrentStepUnchangedEvent
            or SessionCurrentStepRejectedWorkflowEvent
            or SessionCurrentStepRejectedNotActiveEvent => null,
            _ => throw new NotSupportedException(
                $"Unsupported session event type: {@event.GetType().FullName}."),
        };
    }
}
