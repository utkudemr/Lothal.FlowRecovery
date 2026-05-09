using Lothal.FlowRecovery.Modules.Workflow;

namespace Lothal.FlowRecovery.Modules.Workflow.Tests;

public sealed class ValidateWorkflowCurrentStepTests
{
    [Fact]
    public void WorkflowModuleValidateCurrentStep_ShouldAllowInitialStep()
    {
        var module = new WorkflowModule();
        var provider = new TestWorkflowDefinitionProvider(CreateDefinition());
        var definition = provider.Definition ?? throw new InvalidOperationException("Definition is required for this test.");

        var result = module.ValidateCurrentStep(provider, definition.FlowId, null, "Draft");

        Assert.True(result.Success);
        Assert.Equal(ValidateWorkflowCurrentStepOutcome.Allowed, result.Outcome);
        Assert.Null(result.Error);
        Assert.Equal(definition.FlowId, result.FlowId);
        Assert.Null(result.CurrentStep);
        Assert.Equal("Draft", result.TargetStep);
    }

    [Fact]
    public void WorkflowModuleValidateCurrentStep_ShouldAllowInitialStep_WhenTargetStepContainsWhitespace()
    {
        var module = new WorkflowModule();
        var provider = new TestWorkflowDefinitionProvider(CreateDefinition());
        var definition = provider.Definition ?? throw new InvalidOperationException("Definition is required for this test.");

        var result = module.ValidateCurrentStep(provider, definition.FlowId, null, " Draft ");

        Assert.True(result.Success);
        Assert.Equal(ValidateWorkflowCurrentStepOutcome.Allowed, result.Outcome);
        Assert.Null(result.Error);
        Assert.Null(result.CurrentStep);
        Assert.Equal("Draft", result.TargetStep);
    }

    [Fact]
    public void WorkflowModuleValidateCurrentStep_ShouldRejectInvalidTransition()
    {
        var module = new WorkflowModule();
        var provider = new TestWorkflowDefinitionProvider(CreateDefinition());
        var definition = provider.Definition ?? throw new InvalidOperationException("Definition is required for this test.");

        var result = module.ValidateCurrentStep(provider, definition.FlowId, "Draft", "Closed");

        Assert.False(result.Success);
        Assert.Equal(ValidateWorkflowCurrentStepOutcome.Rejected, result.Outcome);
        Assert.Equal("Transition is not allowed.", result.Error);
        Assert.Equal(definition.FlowId, result.FlowId);
        Assert.Equal("Draft", result.CurrentStep);
        Assert.Equal("Closed", result.TargetStep);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldAllow_WhenWorkflowHasNoCurrentStepAndTargetIsStartStep(string? currentStep)
    {
        var provider = new TestWorkflowDefinitionProvider(CreateDefinition());
        var definition = provider.Definition ?? throw new InvalidOperationException("Definition is required for this test.");

        var result = ValidateWorkflowCurrentStep.Validate(provider, definition.FlowId, currentStep, "Draft");

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

    [Theory]
    [InlineData(null)]
    [InlineData("Draft")]
    public void Validate_ShouldReject_WhenTargetStepIsMissing(string? currentStep)
    {
        var provider = new TestWorkflowDefinitionProvider(CreateDefinition());
        var definition = provider.Definition ?? throw new InvalidOperationException("Definition is required for this test.");

        var result = ValidateWorkflowCurrentStep.Validate(provider, definition.FlowId, currentStep, "   ");

        Assert.False(result.Success);
        Assert.Equal(ValidateWorkflowCurrentStepOutcome.Rejected, result.Outcome);
        Assert.Equal("TargetStep is required.", result.Error);
        Assert.Equal(definition.FlowId, result.FlowId);
        Assert.Equal(currentStep?.Trim(), result.CurrentStep);
        Assert.Equal(string.Empty, result.TargetStep);
    }

    [Fact]
    public void Validate_ShouldAllow_WhenTargetStepContainsWhitespace()
    {
        var provider = new TestWorkflowDefinitionProvider(CreateDefinition());
        var definition = provider.Definition ?? throw new InvalidOperationException("Definition is required for this test.");

        var result = ValidateWorkflowCurrentStep.Validate(provider, definition.FlowId, "Draft", " Review ");

        Assert.True(result.Success);
        Assert.Equal(ValidateWorkflowCurrentStepOutcome.Allowed, result.Outcome);
        Assert.Null(result.Error);
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
    public void Validate_ShouldReject_WhenFlowIdIsBlank()
    {
        var provider = new TestWorkflowDefinitionProvider(CreateDefinition());

        var result = ValidateWorkflowCurrentStep.Validate(provider, "   ", "Draft", "Review");

        Assert.False(result.Success);
        Assert.Equal(ValidateWorkflowCurrentStepOutcome.Rejected, result.Outcome);
        Assert.Equal("FlowId is required.", result.Error);
        Assert.Equal(string.Empty, result.FlowId);
        Assert.Equal("Draft", result.CurrentStep);
        Assert.Equal("Review", result.TargetStep);
    }

    [Fact]
    public void Validate_ShouldReject_WhenFlowIdIsNull()
    {
        var provider = new TestWorkflowDefinitionProvider(CreateDefinition());

        var result = ValidateWorkflowCurrentStep.Validate(provider, null, "Draft", "Review");

        Assert.False(result.Success);
        Assert.Equal(ValidateWorkflowCurrentStepOutcome.Rejected, result.Outcome);
        Assert.Equal("FlowId is required.", result.Error);
        Assert.Equal(string.Empty, result.FlowId);
        Assert.Equal("Draft", result.CurrentStep);
        Assert.Equal("Review", result.TargetStep);
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
