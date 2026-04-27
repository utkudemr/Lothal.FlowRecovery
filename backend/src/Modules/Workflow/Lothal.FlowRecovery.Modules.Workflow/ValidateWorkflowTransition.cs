namespace Lothal.FlowRecovery.Modules.Workflow;

public sealed record ValidateWorkflowTransitionResult(
    bool Success,
    string FlowId,
    string CurrentStep,
    string TargetStep,
    ValidateWorkflowTransitionOutcome Outcome,
    string? Error);

public enum ValidateWorkflowTransitionOutcome
{
    Rejected,
    NoOp,
    Allowed,
}

public static class ValidateWorkflowTransition
{
    public static ValidateWorkflowTransitionResult Validate(
        WorkflowDefinition definition,
        string? flowId,
        string? currentStep,
        string? targetStep)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Steps is null || definition.AllowedTransitions is null)
        {
            return new ValidateWorkflowTransitionResult(false, string.Empty, string.Empty, string.Empty, ValidateWorkflowTransitionOutcome.Rejected, "Workflow definition is incomplete.");
        }

        var normalizedFlowId = flowId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedFlowId))
        {
            return new ValidateWorkflowTransitionResult(false, string.Empty, string.Empty, string.Empty, ValidateWorkflowTransitionOutcome.Rejected, "FlowId is required.");
        }

        var normalizedCurrentStep = currentStep?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedCurrentStep))
        {
            return new ValidateWorkflowTransitionResult(false, normalizedFlowId, string.Empty, string.Empty, ValidateWorkflowTransitionOutcome.Rejected, "CurrentStep is required.");
        }

        var normalizedTargetStep = targetStep?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedTargetStep))
        {
            return new ValidateWorkflowTransitionResult(false, normalizedFlowId, normalizedCurrentStep, string.Empty, ValidateWorkflowTransitionOutcome.Rejected, "TargetStep is required.");
        }

        if (!string.Equals(definition.FlowId, normalizedFlowId, StringComparison.Ordinal))
        {
            return new ValidateWorkflowTransitionResult(false, normalizedFlowId, normalizedCurrentStep, normalizedTargetStep, ValidateWorkflowTransitionOutcome.Rejected, "Workflow definition does not match FlowId.");
        }

        if (!ContainsStep(definition.Steps, normalizedCurrentStep))
        {
            return new ValidateWorkflowTransitionResult(false, normalizedFlowId, normalizedCurrentStep, normalizedTargetStep, ValidateWorkflowTransitionOutcome.Rejected, "CurrentStep is not defined.");
        }

        if (!ContainsStep(definition.Steps, normalizedTargetStep))
        {
            return new ValidateWorkflowTransitionResult(false, normalizedFlowId, normalizedCurrentStep, normalizedTargetStep, ValidateWorkflowTransitionOutcome.Rejected, "TargetStep is not defined.");
        }

        if (string.Equals(normalizedCurrentStep, normalizedTargetStep, StringComparison.Ordinal))
        {
            return new ValidateWorkflowTransitionResult(true, normalizedFlowId, normalizedCurrentStep, normalizedTargetStep, ValidateWorkflowTransitionOutcome.NoOp, null);
        }

        if (definition.AllowedTransitions.TryGetValue(normalizedCurrentStep, out var allowedTargets))
        {
            if (allowedTargets is null)
            {
                return new ValidateWorkflowTransitionResult(false, normalizedFlowId, normalizedCurrentStep, normalizedTargetStep, ValidateWorkflowTransitionOutcome.Rejected, "Workflow definition is incomplete.");
            }

            if (ContainsStep(allowedTargets, normalizedTargetStep))
            {
                return new ValidateWorkflowTransitionResult(true, normalizedFlowId, normalizedCurrentStep, normalizedTargetStep, ValidateWorkflowTransitionOutcome.Allowed, null);
            }
        }

        return new ValidateWorkflowTransitionResult(false, normalizedFlowId, normalizedCurrentStep, normalizedTargetStep, ValidateWorkflowTransitionOutcome.Rejected, "Transition is not allowed.");
    }

    private static bool ContainsStep(IReadOnlyCollection<string> steps, string step)
    {
        foreach (var candidate in steps)
        {
            if (string.Equals(candidate?.Trim(), step, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
