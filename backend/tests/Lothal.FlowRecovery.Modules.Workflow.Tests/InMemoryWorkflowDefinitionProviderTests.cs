using Lothal.FlowRecovery.Modules.Workflow;

namespace Lothal.FlowRecovery.Modules.Workflow.Tests;

public sealed class InMemoryWorkflowDefinitionProviderTests
{
    [Fact]
    public void GetDefinition_ShouldReturnMatchingDefinition()
    {
        var definition = CreateDefinition("flow-a");
        var provider = new InMemoryWorkflowDefinitionProvider(definition);

        var result = provider.GetDefinition("flow-a");

        Assert.Same(definition, result);
    }

    [Fact]
    public void GetDefinition_ShouldReturnNull_WhenDefinitionIsMissing()
    {
        var provider = new InMemoryWorkflowDefinitionProvider(CreateDefinition("flow-a"));

        var result = provider.GetDefinition("flow-b");

        Assert.Null(result);
    }

    [Theory]
    [InlineData(" flow-a ", "flow-a")]
    [InlineData("\tflow-a\n", "flow-a")]
    public void GetDefinition_ShouldTrimFlowId_WhenLookingUpDefinition(string requestedFlowId, string expectedFlowId)
    {
        var definition = CreateDefinition(expectedFlowId);
        var provider = new InMemoryWorkflowDefinitionProvider(definition);

        var result = provider.GetDefinition(requestedFlowId);

        Assert.Same(definition, result);
        Assert.Equal(expectedFlowId, result?.FlowId);
    }

    [Fact]
    public void GetDefinition_ShouldMatchFlowIdCaseInsensitively()
    {
        var definition = CreateDefinition("FLOW-A");
        var provider = new InMemoryWorkflowDefinitionProvider(definition);

        var result = provider.GetDefinition("flow-a");

        Assert.Same(definition, result);
        Assert.Equal("FLOW-A", result?.FlowId);
    }

    [Fact]
    public void Constructor_ShouldRejectDuplicateFlowIdsAfterNormalization()
    {
        var first = CreateDefinition("flow-a");
        var second = CreateDefinition("FLOW-A");

        var exception = Assert.Throws<ArgumentException>(() => new InMemoryWorkflowDefinitionProvider(first, second));

        Assert.Contains("Duplicate workflow definition for FlowId 'FLOW-A' (case-insensitive match).", exception.Message);
    }

    [Theory]
    [InlineData(" flow-a")]
    [InlineData("flow-a ")]
    [InlineData("\tflow-a")]
    public void Constructor_ShouldRejectNonCanonicalFlowIds(string flowId)
    {
        var definition = CreateDefinition(flowId);

        var exception = Assert.Throws<ArgumentException>(() => new InMemoryWorkflowDefinitionProvider(definition));

        Assert.Contains("must not contain leading or trailing whitespace", exception.Message);
    }

    private static WorkflowDefinition CreateDefinition(string flowId)
    {
        return new WorkflowDefinition(
            flowId,
            new[] { "Draft", "Review", "Closed" },
            new Dictionary<string, IReadOnlyCollection<string>>
            {
                ["Draft"] = new[] { "Review" },
                ["Review"] = new[] { "Closed" },
                ["Closed"] = Array.Empty<string>(),
            });
    }
}
