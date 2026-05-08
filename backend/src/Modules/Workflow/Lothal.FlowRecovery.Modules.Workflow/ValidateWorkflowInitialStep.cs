namespace Lothal.FlowRecovery.Modules.Workflow;

public sealed record ValidateWorkflowInitialStepResult(
    bool Success,
    string FlowId,
    string TargetStep,
    ValidateWorkflowInitialStepOutcome Outcome,
    string? Error);

public enum ValidateWorkflowInitialStepOutcome
{
    Rejected,
    Allowed,
}

public static class ValidateWorkflowInitialStep
{
    public static ValidateWorkflowInitialStepResult Validate(
        WorkflowDefinition definition,
        string? flowId,
        string? targetStep)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Steps is null || definition.AllowedTransitions is null)
        {
            return new ValidateWorkflowInitialStepResult(false, string.Empty, string.Empty, ValidateWorkflowInitialStepOutcome.Rejected, "Workflow definition is incomplete.");
        }

        var normalizedFlowId = flowId?.Trim() ?? string.Empty;
        var normalizedTargetStep = targetStep?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedFlowId))
        {
            return new ValidateWorkflowInitialStepResult(false, string.Empty, normalizedTargetStep, ValidateWorkflowInitialStepOutcome.Rejected, "FlowId is required.");
        }

        if (string.IsNullOrWhiteSpace(normalizedTargetStep))
        {
            return new ValidateWorkflowInitialStepResult(false, normalizedFlowId, string.Empty, ValidateWorkflowInitialStepOutcome.Rejected, "TargetStep is required.");
        }

        if (!string.Equals(definition.FlowId, normalizedFlowId, StringComparison.Ordinal))
        {
            return new ValidateWorkflowInitialStepResult(false, normalizedFlowId, normalizedTargetStep, ValidateWorkflowInitialStepOutcome.Rejected, "Workflow definition does not match FlowId.");
        }

        if (!ContainsStep(definition.Steps, normalizedTargetStep))
        {
            return new ValidateWorkflowInitialStepResult(false, normalizedFlowId, normalizedTargetStep, ValidateWorkflowInitialStepOutcome.Rejected, "TargetStep is not defined.");
        }

        if (!TryGetWorkflowStartStep(definition, out var workflowStartStep))
        {
            return new ValidateWorkflowInitialStepResult(false, normalizedFlowId, normalizedTargetStep, ValidateWorkflowInitialStepOutcome.Rejected, "Workflow definition is incomplete.");
        }

        if (!string.Equals(workflowStartStep, normalizedTargetStep, StringComparison.Ordinal))
        {
            return new ValidateWorkflowInitialStepResult(false, normalizedFlowId, normalizedTargetStep, ValidateWorkflowInitialStepOutcome.Rejected, "TargetStep must be workflow start step.");
        }

        return new ValidateWorkflowInitialStepResult(true, normalizedFlowId, normalizedTargetStep, ValidateWorkflowInitialStepOutcome.Allowed, null);
    }

    private static bool TryGetWorkflowStartStep(WorkflowDefinition workflowDefinition, out string? workflowStartStep)
    {
        workflowStartStep = null;

        var definedSteps = new HashSet<string>(StringComparer.Ordinal);
        var orderedSteps = new List<string>();

        foreach (var candidate in workflowDefinition.Steps)
        {
            var normalized = candidate?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (definedSteps.Add(normalized))
            {
                orderedSteps.Add(normalized);
            }
        }

        if (orderedSteps.Count == 0)
        {
            return false;
        }

        var incomingSteps = new HashSet<string>(StringComparer.Ordinal);
        foreach (var transition in workflowDefinition.AllowedTransitions)
        {
            var normalizedSourceStep = transition.Key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedSourceStep) || !definedSteps.Contains(normalizedSourceStep))
            {
                return false;
            }

            var allowedTargets = transition.Value;
            if (allowedTargets is null)
            {
                return false;
            }

            foreach (var target in allowedTargets)
            {
                var normalizedTarget = target?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedTarget))
                {
                    return false;
                }

                if (!definedSteps.Contains(normalizedTarget))
                {
                    return false;
                }

                incomingSteps.Add(normalizedTarget);
            }
        }

        foreach (var candidate in orderedSteps)
        {
            if (incomingSteps.Contains(candidate))
            {
                continue;
            }

            if (workflowStartStep is not null)
            {
                workflowStartStep = null;
                return false;
            }

            workflowStartStep = candidate;
        }

        return workflowStartStep is not null;
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
