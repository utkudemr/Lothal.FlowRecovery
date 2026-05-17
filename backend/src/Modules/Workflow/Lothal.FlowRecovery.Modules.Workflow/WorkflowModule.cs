namespace Lothal.FlowRecovery.Modules.Workflow;

public sealed class WorkflowModule
{
    public ValidateWorkflowInitialStepResult ValidateInitialStep(
        WorkflowDefinition definition,
        string? flowId,
        string? targetStep)
    {
        return ValidateWorkflowInitialStep.Validate(definition, flowId, targetStep);
    }

    public WorkflowStartStepQueryResult QueryStartStep(WorkflowDefinition definition)
    {
        return ValidateWorkflowInitialStep.QueryStartStep(definition);
    }

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
