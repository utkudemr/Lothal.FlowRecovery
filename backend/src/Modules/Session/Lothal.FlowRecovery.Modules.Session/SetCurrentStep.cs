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
    NotFound = 0,
    NotActive = 1,
    Unchanged = 2,
    Changed = 3,
    Rejected = 4,
}

internal sealed class SetCurrentStepHandler
{
    private readonly InMemorySessionStore _store;
    private readonly ISessionCurrentStepValidator _currentStepValidator;

    public SetCurrentStepHandler(InMemorySessionStore store, ISessionCurrentStepValidator currentStepValidator)
    {
        _store = store;
        _currentStepValidator = currentStepValidator;
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

        if (!SessionCurrentStepMetadata.TryCreate(
                command.ChangedBy,
                command.ActorType,
                command.Reason,
                out var metadata,
                out var metadataError))
        {
            return new SetCurrentStepResult(false, command.SessionId, string.Empty, "Rejected", null, metadataError, null, null);
        }

        if (!_store.TryGetSnapshot(command.SessionId, out var snapshot) || snapshot is null)
        {
            return new SetCurrentStepResult(false, command.SessionId, string.Empty, "Rejected", null, "Session not found.", SetCurrentStepOutcome.NotFound, null);
        }

        if (metadata!.ActorType == "Operator" &&
            metadata.Reason is null)
        {
            return new SetCurrentStepResult(false, command.SessionId, string.Empty, "Rejected", null, "Reason is required for operator step change.", null, null);
        }

        var outcome = _store.TrySetCurrentStep(
            command.SessionId,
            _currentStepValidator,
            currentStep,
            metadata,
            out var session,
            out var stepSetEvent,
            out var error);
        if (outcome == SetCurrentStepOutcome.NotActive)
        {
            error = metadata.ActorType == "Operator" && metadata.Reason is null
                ? "Reason is required for operator step change."
                : "Session is not active.";

            return new SetCurrentStepResult(false, session!.SessionId, session.FlowId, session.Status, session.CurrentStep, error, outcome, null);
        }

        if (outcome == SetCurrentStepOutcome.Rejected)
        {
            return new SetCurrentStepResult(false, session!.SessionId, session.FlowId, session.Status, session.CurrentStep, error, outcome, null);
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
}
