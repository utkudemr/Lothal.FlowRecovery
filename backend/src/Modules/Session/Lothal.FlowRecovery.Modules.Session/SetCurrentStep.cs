namespace Lothal.FlowRecovery.Modules.Session;

public sealed record SetCurrentStepCommand(
    Guid SessionId,
    string CurrentStep,
    string ChangedBy,
    string ActorType,
    string? Reason);

public sealed record SetCurrentStepResult(
    bool Success,
    Guid SessionId,
    string FlowId,
    string Status,
    string? CurrentStep,
    string? Error,
    SetCurrentStepOutcome? Outcome,
    SessionNotification? Notification);

public enum SetCurrentStepOutcome
{
    NotFound,
    NotActive,
    Unchanged,
    Changed,
}

internal sealed class SetCurrentStepHandler
{
    private readonly InMemorySessionStore _store;

    public SetCurrentStepHandler(InMemorySessionStore store)
    {
        _store = store;
    }

    public SetCurrentStepResult Handle(SetCurrentStepCommand command)
    {
        if (command.SessionId == Guid.Empty)
        {
            return new SetCurrentStepResult(false, command.SessionId, string.Empty, "Rejected", null, "SessionId is required.", null, null);
        }

        var currentStep = command.CurrentStep?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(currentStep))
        {
            return new SetCurrentStepResult(false, command.SessionId, string.Empty, "Rejected", null, "CurrentStep is required.", null, null);
        }

        var changedBy = command.ChangedBy?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(changedBy))
        {
            return new SetCurrentStepResult(false, command.SessionId, string.Empty, "Rejected", null, "ChangedBy is required.", null, null);
        }

        var trimmedActorType = command.ActorType?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedActorType))
        {
            return new SetCurrentStepResult(false, command.SessionId, string.Empty, "Rejected", null, "ActorType is required.", null, null);
        }

        var actorType = NormalizeActorType(trimmedActorType);
        if (actorType is null)
        {
            return new SetCurrentStepResult(false, command.SessionId, string.Empty, "Rejected", null, "ActorType is invalid.", null, null);
        }

        if (actorType == "Operator" &&
            string.IsNullOrWhiteSpace(command.Reason) &&
            _store.TryGetSnapshot(command.SessionId, out var snapshot) &&
            snapshot is not null &&
            snapshot.Status == "Active")
        {
            return new SetCurrentStepResult(false, command.SessionId, string.Empty, "Rejected", null, "Reason is required for operator step change.", null, null);
        }

        var reason = command.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = null;
        }

        var outcome = _store.TrySetCurrentStep(
            command.SessionId,
            currentStep,
            changedBy,
            actorType,
            reason,
            out var session,
            out var stepSetEvent);
        if (outcome == SetCurrentStepOutcome.NotFound)
        {
            return new SetCurrentStepResult(false, command.SessionId, string.Empty, "Rejected", null, "Session not found.", outcome, null);
        }

        if (outcome == SetCurrentStepOutcome.NotActive)
        {
            return new SetCurrentStepResult(false, session!.SessionId, session.FlowId, session.Status, session.CurrentStep, "Session is not active.", outcome, null);
        }

        SessionNotification? notification = null;
        if (outcome == SetCurrentStepOutcome.Changed)
        {
            notification = stepSetEvent is not null
                ? SessionNotificationMapper.Map(stepSetEvent)
                : null;

            if (notification is null)
            {
                throw new InvalidOperationException("Invariant violation: changed outcome must produce a step-changed notification.");
            }
        }

        return new SetCurrentStepResult(true, session!.SessionId, session.FlowId, session.Status, session.CurrentStep, null, outcome, notification);
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
