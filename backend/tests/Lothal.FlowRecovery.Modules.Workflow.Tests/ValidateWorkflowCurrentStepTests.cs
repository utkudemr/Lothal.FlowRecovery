using Lothal.FlowRecovery.Modules.Workflow;

namespace Lothal.FlowRecovery.Modules.Workflow.Tests;

public sealed class ValidateWorkflowCurrentStepTests
{
    [Fact]
    public void Validate_ShouldAllow_WhenWorkflowHasNoCurrentStepAndTargetIsStartStep()
    {
        var provider = new TestWorkflowDefinitionProvider(CreateDefinition());
        var definition = provider.Definition ?? throw new InvalidOperationException("Definition is required for this test.");

        var result = ValidateWorkflowCurrentStep.Validate(provider, definition.FlowId, null, "Draft");

        Assert.True(result.Success);
        Assert.Equal(ValidateWorkflowCurrentStepOutcome.Allowed, result.Outcome);
        Assert.Null(result.Error);
        Assert.Equal(provider.Definition.FlowId, result.FlowId);
        Assert.Null(result.CurrentStep);
        Assert.Equal("Draft", result.TargetStep);
    }

    [Fact]
    public void Validate_ShouldAllow_WhenFlowIdContainsWhitespace()
    {
        var provider = new TestWorkflowDefinitionProvider(CreateDefinition("flow-a"));

        var result = ValidateWorkflowCurrentStep.Validate(provider, " flow-a ", "Draft", "Review");

        Assert.True(result.Success);
        Assert.Equal(ValidateWorkflowCurrentStepOutcome.Allowed, result.Outcome);
        Assert.Null(result.Error);
        Assert.Equal("flow-a", result.FlowId);
        Assert.Equal("Draft", result.CurrentStep);
        Assert.Equal("Review", result.TargetStep);
    }

    [Fact]
    public void Validate_ShouldReject_WhenFlowIdContainsWhitespaceAndTransitionIsNotAllowed()
    {
        var provider = new TestWorkflowDefinitionProvider(CreateDefinition("flow-a"));

        var result = ValidateWorkflowCurrentStep.Validate(provider, " flow-a ", "Draft", "Closed");

        Assert.False(result.Success);
        Assert.Equal(ValidateWorkflowCurrentStepOutcome.Rejected, result.Outcome);
        Assert.Equal("Transition is not allowed.", result.Error);
        Assert.Equal("flow-a", result.FlowId);
        Assert.Equal("Draft", result.CurrentStep);
        Assert.Equal("Closed", result.TargetStep);
    }

    [Fact]
    public void Validate_ShouldAllow_WhenWorkflowTransitionIsAllowed()
    {
        var provider = new TestWorkflowDefinitionProvider(CreateDefinition());
        var definition = provider.Definition ?? throw new InvalidOperationException("Definition is required for this test.");

        var result = ValidateWorkflowCurrentStep.Validate(provider, definition.FlowId, "Draft", "Review");

        Assert.True(result.Success);
        Assert.Equal(ValidateWorkflowCurrentStepOutcome.Allowed, result.Outcome);
        Assert.Null(result.Error);
        Assert.Equal(provider.Definition.FlowId, result.FlowId);
        Assert.Equal("Draft", result.CurrentStep);
        Assert.Equal("Review", result.TargetStep);
    }

    [Fact]
    public void Validate_ShouldReturnNoOp_WhenCurrentAndTargetStepsMatch()
    {
        var provider = new TestWorkflowDefinitionProvider(CreateDefinition());
        var definition = provider.Definition ?? throw new InvalidOperationException("Definition is required for this test.");

        var result = ValidateWorkflowCurrentStep.Validate(provider, definition.FlowId, "Draft", "Draft");

        Assert.True(result.Success);
        Assert.Equal(ValidateWorkflowCurrentStepOutcome.NoOp, result.Outcome);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Validate_ShouldReject_WhenWorkflowDefinitionIsMissing()
    {
        var provider = new TestWorkflowDefinitionProvider(null);

        var result = ValidateWorkflowCurrentStep.Validate(provider, "flow-1", "Draft", "Review");

        Assert.False(result.Success);
        Assert.Equal(ValidateWorkflowCurrentStepOutcome.Rejected, result.Outcome);
        Assert.Equal("Workflow definition not found.", result.Error);
        Assert.Equal("flow-1", result.FlowId);
    }

    [Fact]
    public void Validate_ShouldRejectInitialStep_WhenTargetIsNotWorkflowStartStep()
    {
        var provider = new TestWorkflowDefinitionProvider(CreateDefinition());
        var definition = provider.Definition ?? throw new InvalidOperationException("Definition is required for this test.");

        var result = ValidateWorkflowCurrentStep.Validate(provider, definition.FlowId, null, "Review");

        Assert.False(result.Success);
        Assert.Equal(ValidateWorkflowCurrentStepOutcome.Rejected, result.Outcome);
        Assert.Equal("TargetStep must be workflow start step.", result.Error);
    }

    private static WorkflowDefinition CreateDefinition(string? flowId = null)
    {
        flowId ??= $"flow-{Guid.NewGuid():N}";
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

    private sealed class TestWorkflowDefinitionProvider : IWorkflowDefinitionProvider
    {
        public TestWorkflowDefinitionProvider(WorkflowDefinition? definition)
        {
            Definition = definition;
        }

        public WorkflowDefinition? Definition { get; }

        public WorkflowDefinition? GetDefinition(string flowId)
        {
            if (Definition is null)
            {
                return null;
            }

            return string.Equals(Definition.FlowId, flowId, StringComparison.Ordinal)
                ? Definition
                : null;
        }
    }
}
