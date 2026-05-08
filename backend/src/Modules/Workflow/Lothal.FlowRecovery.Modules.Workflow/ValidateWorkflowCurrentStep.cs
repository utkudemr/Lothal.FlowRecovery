namespace Lothal.FlowRecovery.Modules.Workflow;

public sealed record ValidateWorkflowCurrentStepResult(
    bool Success,
    string FlowId,
    string? CurrentStep,
    string TargetStep,
    ValidateWorkflowCurrentStepOutcome Outcome,
    string? Error);

public enum ValidateWorkflowCurrentStepOutcome
{
    Rejected,
    NoOp,
    Allowed,
}

public static class ValidateWorkflowCurrentStep
{
    public static ValidateWorkflowCurrentStepResult Validate(
        IWorkflowDefinitionProvider workflowDefinitions,
        string? flowId,
        string? currentStep,
        string? targetStep)
    {
        ArgumentNullException.ThrowIfNull(workflowDefinitions);

        var normalizedFlowId = flowId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedFlowId))
        {
            return new ValidateWorkflowCurrentStepResult(
                false,
                string.Empty,
                currentStep?.Trim(),
                targetStep?.Trim() ?? string.Empty,
                ValidateWorkflowCurrentStepOutcome.Rejected,
                "FlowId is required.");
        }

        var workflowDefinition = workflowDefinitions.GetDefinition(normalizedFlowId);
        if (workflowDefinition is null)
        {
            return new ValidateWorkflowCurrentStepResult(
                false,
                normalizedFlowId,
                currentStep?.Trim(),
                targetStep?.Trim() ?? string.Empty,
                ValidateWorkflowCurrentStepOutcome.Rejected,
                "Workflow definition not found.");
        }

        var normalizedTargetStep = targetStep?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(currentStep))
        {
            var initialStepValidation = ValidateWorkflowInitialStep.Validate(workflowDefinition, normalizedFlowId, normalizedTargetStep);
            return initialStepValidation.Outcome == ValidateWorkflowInitialStepOutcome.Rejected
                ? new ValidateWorkflowCurrentStepResult(
                    false,
                    initialStepValidation.FlowId,
                    null,
                    initialStepValidation.TargetStep,
                    ValidateWorkflowCurrentStepOutcome.Rejected,
                    initialStepValidation.Error ?? "Workflow transition rejected.")
                : new ValidateWorkflowCurrentStepResult(
                    true,
                    initialStepValidation.FlowId,
                    null,
                    initialStepValidation.TargetStep,
                    ValidateWorkflowCurrentStepOutcome.Allowed,
                    null);
        }

        var transitionValidation = ValidateWorkflowTransition.Validate(workflowDefinition, normalizedFlowId, currentStep, normalizedTargetStep);
        return transitionValidation.Outcome == ValidateWorkflowTransitionOutcome.Rejected
            ? new ValidateWorkflowCurrentStepResult(
                false,
                transitionValidation.FlowId,
                transitionValidation.CurrentStep,
                transitionValidation.TargetStep,
                ValidateWorkflowCurrentStepOutcome.Rejected,
                transitionValidation.Error ?? "Workflow transition rejected.")
            : new ValidateWorkflowCurrentStepResult(
                true,
                transitionValidation.FlowId,
                transitionValidation.CurrentStep,
                transitionValidation.TargetStep,
                transitionValidation.Outcome == ValidateWorkflowTransitionOutcome.NoOp
                    ? ValidateWorkflowCurrentStepOutcome.NoOp
                    : ValidateWorkflowCurrentStepOutcome.Allowed,
                null);
    }
}
