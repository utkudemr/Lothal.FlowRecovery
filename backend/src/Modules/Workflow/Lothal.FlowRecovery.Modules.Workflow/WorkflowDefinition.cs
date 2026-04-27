namespace Lothal.FlowRecovery.Modules.Workflow;

public sealed record WorkflowDefinition(
    string FlowId,
    IReadOnlyCollection<string> Steps,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> AllowedTransitions);
