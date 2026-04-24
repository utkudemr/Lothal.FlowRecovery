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

public static class SessionNotificationMapper
{
    public static SessionNotification? Map(SessionEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return @event switch
        {
            SessionCurrentStepSetEvent currentStepSetEvent => new StepChangedNotification(
                currentStepSetEvent.SessionId,
                currentStepSetEvent.FlowId,
                currentStepSetEvent.CurrentStep,
                currentStepSetEvent.PreviousStep,
                currentStepSetEvent.ChangedBy,
                currentStepSetEvent.ActorType,
                currentStepSetEvent.Reason,
                currentStepSetEvent.OccurredAtUtc),
            _ => throw new NotSupportedException($"Unsupported session event type: {@event.GetType().Name}."),
        };
    }
}
