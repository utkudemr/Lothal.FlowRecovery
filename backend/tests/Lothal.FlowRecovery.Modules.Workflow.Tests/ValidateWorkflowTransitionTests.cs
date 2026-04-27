using Lothal.FlowRecovery.Modules.Workflow;

namespace Lothal.FlowRecovery.Modules.Workflow.Tests;

public sealed class ValidateWorkflowTransitionTests
{
    [Fact]
    public void ValidateTransition_ShouldAllow_WhenTransitionIsAllowed()
    {
        var module = new WorkflowModule();
        var definition = CreateDefinition();

        var result = module.ValidateTransition(definition, definition.FlowId, "Draft", "Review");

        Assert.True(result.Success);
        Assert.Equal(ValidateWorkflowTransitionOutcome.Allowed, result.Outcome);
        Assert.Null(result.Error);
        Assert.Equal(definition.FlowId, result.FlowId);
        Assert.Equal("Draft", result.CurrentStep);
        Assert.Equal("Review", result.TargetStep);
    }

    [Fact]
    public void ValidateTransition_ShouldReject_WhenTransitionIsNotAllowed()
    {
        var module = new WorkflowModule();
        var definition = CreateDefinition();

        var result = module.ValidateTransition(definition, definition.FlowId, "Draft", "Closed");

        Assert.False(result.Success);
        Assert.Equal(ValidateWorkflowTransitionOutcome.Rejected, result.Outcome);
        Assert.Equal("Transition is not allowed.", result.Error);
    }

    [Fact]
    public void ValidateTransition_ShouldReturnNoOp_WhenCurrentAndTargetStepsMatch()
    {
        var module = new WorkflowModule();
        var definition = CreateDefinition();

        var result = module.ValidateTransition(definition, definition.FlowId, "Draft", "Draft");

        Assert.True(result.Success);
        Assert.Equal(ValidateWorkflowTransitionOutcome.NoOp, result.Outcome);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateTransition_ShouldReject_WhenFlowIdIsMissing(string? flowId)
    {
        var module = new WorkflowModule();
        var definition = CreateDefinition();

        var result = module.ValidateTransition(definition, flowId, "Draft", "Review");

        Assert.False(result.Success);
        Assert.Equal(ValidateWorkflowTransitionOutcome.Rejected, result.Outcome);
        Assert.Equal("FlowId is required.", result.Error);
        Assert.Equal(string.Empty, result.FlowId);
    }

    [Fact]
    public void ValidateTransition_ShouldReject_WhenCurrentStepIsUndefined()
    {
        var module = new WorkflowModule();
        var definition = CreateDefinition();

        var result = module.ValidateTransition(definition, definition.FlowId, "Unknown", "Review");

        Assert.False(result.Success);
        Assert.Equal(ValidateWorkflowTransitionOutcome.Rejected, result.Outcome);
        Assert.Equal("CurrentStep is not defined.", result.Error);
    }

    [Fact]
    public void ValidateTransition_ShouldReject_WhenTargetStepIsUndefined()
    {
        var module = new WorkflowModule();
        var definition = CreateDefinition();

        var result = module.ValidateTransition(definition, definition.FlowId, "Draft", "Unknown");

        Assert.False(result.Success);
        Assert.Equal(ValidateWorkflowTransitionOutcome.Rejected, result.Outcome);
        Assert.Equal("TargetStep is not defined.", result.Error);
    }

    [Fact]
    public void ValidateTransition_ShouldReject_WhenFlowIdDoesNotMatchDefinition()
    {
        var module = new WorkflowModule();
        var definition = CreateDefinition(flowId: "flow-a");

        var result = module.ValidateTransition(definition, "flow-b", "Draft", "Review");

        Assert.False(result.Success);
        Assert.Equal(ValidateWorkflowTransitionOutcome.Rejected, result.Outcome);
        Assert.Equal("Workflow definition does not match FlowId.", result.Error);
    }

    [Fact]
    public void ValidateTransition_ShouldReject_WhenAllowedTargetsCollectionIsNull()
    {
        var module = new WorkflowModule();
        var definition = CreateDefinition(useNullAllowedTargets: true);

        var result = module.ValidateTransition(definition, definition.FlowId, "Draft", "Review");

        Assert.False(result.Success);
        Assert.Equal(ValidateWorkflowTransitionOutcome.Rejected, result.Outcome);
        Assert.Equal("Workflow definition is incomplete.", result.Error);
    }

    private static WorkflowDefinition CreateDefinition(string? flowId = null, IReadOnlyCollection<string>? allowedTargets = null, bool useNullAllowedTargets = false)
    {
        var resolvedFlowId = flowId ?? $"flow-{Guid.NewGuid():N}";
        var steps = new[] { "Draft", "Review", "Closed" };
        var transitions = new Dictionary<string, IReadOnlyCollection<string>>
        {
            ["Draft"] = useNullAllowedTargets ? null! : allowedTargets ?? new[] { "Review" },
        };

        return new WorkflowDefinition(resolvedFlowId, steps, transitions);
    }
}
