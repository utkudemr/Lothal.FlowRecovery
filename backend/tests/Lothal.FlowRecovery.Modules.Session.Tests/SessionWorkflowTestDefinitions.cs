using Lothal.FlowRecovery.Modules.Workflow;

namespace Lothal.FlowRecovery.Modules.Session.Tests;

internal static class SessionWorkflowTestDefinitions
{
    public static SessionModule CreateModule(string flowId)
    {
        return CreateModule(CreateCheckoutWorkflow(flowId));
    }

    public static SessionModule CreateModule(params WorkflowDefinition[] definitions)
    {
        return new SessionModule(new TestWorkflowDefinitionProvider(definitions));
    }

    public static WorkflowDefinition CreateCheckoutWorkflow(string flowId)
    {
        return CreateWorkflow(
            flowId,
            new[] { "cart", "payment", "review", "confirm" },
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
            {
                ["cart"] = new[] { "payment" },
                ["payment"] = new[] { "review" },
                ["review"] = new[] { "confirm" },
                ["confirm"] = Array.Empty<string>(),
            });
    }

    public static WorkflowDefinition CreateWorkflow(
        string flowId,
        IReadOnlyCollection<string> steps,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> allowedTransitions)
    {
        return new WorkflowDefinition(flowId, steps, allowedTransitions);
    }

    private sealed class TestWorkflowDefinitionProvider : IWorkflowDefinitionProvider
    {
        private readonly Dictionary<string, WorkflowDefinition> _definitions;

        public TestWorkflowDefinitionProvider(IEnumerable<WorkflowDefinition> definitions)
        {
            _definitions = definitions.ToDictionary(definition => definition.FlowId, StringComparer.Ordinal);
        }

        public WorkflowDefinition? GetDefinition(string flowId)
        {
            return _definitions.TryGetValue(flowId, out var definition)
                ? definition
                : null;
        }
    }
}
