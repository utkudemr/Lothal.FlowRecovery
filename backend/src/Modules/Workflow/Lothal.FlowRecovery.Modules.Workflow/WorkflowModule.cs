namespace Lothal.FlowRecovery.Modules.Workflow;

public sealed class WorkflowModule
{
    public ValidateWorkflowCurrentStepResult ValidateCurrentStep(
        IWorkflowDefinitionProvider workflowDefinitions,
        string? flowId,
        string? currentStep,
        string? targetStep)
    {
        return ValidateWorkflowCurrentStep.Validate(workflowDefinitions, flowId, currentStep, targetStep);
    }

    public ValidateWorkflowTransitionResult ValidateTransition(
        WorkflowDefinition definition,
        string? flowId,
        string? currentStep,
        string? targetStep)
    {
        return ValidateWorkflowTransition.Validate(definition, flowId, currentStep, targetStep);
    }
}
