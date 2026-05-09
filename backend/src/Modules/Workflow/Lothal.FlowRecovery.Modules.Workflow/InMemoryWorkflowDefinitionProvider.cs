namespace Lothal.FlowRecovery.Modules.Workflow;

public sealed class InMemoryWorkflowDefinitionProvider : IWorkflowDefinitionProvider
{
    private readonly IReadOnlyDictionary<string, WorkflowDefinition> _definitions;

    public InMemoryWorkflowDefinitionProvider(params WorkflowDefinition[] definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        if (definitions.Length == 0)
        {
            throw new ArgumentException("At least one workflow definition is required.", nameof(definitions));
        }

        var definitionMap = new Dictionary<string, WorkflowDefinition>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);

            var flowId = definition.FlowId ?? string.Empty;
            var normalizedFlowId = flowId.Trim();
            if (string.IsNullOrWhiteSpace(normalizedFlowId))
            {
                throw new ArgumentException("Workflow definition FlowId is required.", nameof(definitions));
            }

            if (!string.Equals(flowId, normalizedFlowId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Workflow definition FlowId must not contain leading or trailing whitespace.", nameof(definitions));
            }

            if (!definitionMap.TryAdd(normalizedFlowId, definition))
            {
                throw new ArgumentException($"Duplicate workflow definition for FlowId '{normalizedFlowId}'.", nameof(definitions));
            }
        }

        _definitions = definitionMap;
    }

    public WorkflowDefinition? GetDefinition(string flowId)
    {
        if (string.IsNullOrWhiteSpace(flowId))
        {
            return null;
        }

        var normalizedFlowId = flowId.Trim();
        return _definitions.TryGetValue(normalizedFlowId, out var definition) ? definition : null;
    }
}
