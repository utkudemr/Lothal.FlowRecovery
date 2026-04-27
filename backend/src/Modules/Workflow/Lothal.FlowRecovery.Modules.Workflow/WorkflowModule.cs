namespace Lothal.FlowRecovery.Modules.Workflow;

public sealed class WorkflowModule
{
    public ValidateWorkflowTransitionResult ValidateTransition(
        WorkflowDefinition definition,
        string? flowId,
        string? currentStep,
        string? targetStep)
    {
        return ValidateWorkflowTransition.Validate(definition, flowId, currentStep, targetStep);
    }
}
