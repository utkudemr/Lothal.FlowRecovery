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
    SetCurrentStepOutcome? Outcome);

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
            return new SetCurrentStepResult(false, command.SessionId, string.Empty, "Rejected", null, "SessionId is required.", null);
        }

        var currentStep = command.CurrentStep?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(currentStep))
        {
            return new SetCurrentStepResult(false, command.SessionId, string.Empty, "Rejected", null, "CurrentStep is required.", null);
        }

        var changedBy = command.ChangedBy?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(changedBy))
        {
            return new SetCurrentStepResult(false, command.SessionId, string.Empty, "Rejected", null, "ChangedBy is required.", null);
        }

        var trimmedActorType = command.ActorType?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedActorType))
        {
            return new SetCurrentStepResult(false, command.SessionId, string.Empty, "Rejected", null, "ActorType is required.", null);
        }

        var actorType = NormalizeActorType(trimmedActorType);
        if (actorType is null)
        {
            return new SetCurrentStepResult(false, command.SessionId, string.Empty, "Rejected", null, "ActorType is invalid.", null);
        }

        var reason = command.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = null;
        }

        var outcome = _store.TrySetCurrentStep(command.SessionId, currentStep, changedBy, actorType, reason, out var session);
        if (outcome == SetCurrentStepOutcome.NotFound)
        {
            return new SetCurrentStepResult(false, command.SessionId, string.Empty, "Rejected", null, "Session not found.", outcome);
        }

        if (outcome == SetCurrentStepOutcome.NotActive)
        {
            return new SetCurrentStepResult(false, session!.SessionId, session.FlowId, session.Status, session.CurrentStep, "Session is not active.", outcome);
        }

        return new SetCurrentStepResult(true, session!.SessionId, session.FlowId, session.Status, session.CurrentStep, null, outcome);
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
