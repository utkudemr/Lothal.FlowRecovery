using System.Reflection;
using Lothal.FlowRecovery.Modules.Session;
using static Lothal.FlowRecovery.Modules.Session.Tests.SessionWorkflowTestDefinitions;

namespace Lothal.FlowRecovery.Modules.Session.Tests;

public sealed class SetCurrentStepTests
{
    [Fact]
    public void SetCurrentStep_ShouldSetActiveSession_AndAppendSessionCurrentStepSetEvent()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));
        var beforeSetUtc = DateTime.UtcNow;

        var result = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, " cart ", "operator-b", "oPeRaToR", "  manual correction  "));

        var afterSetUtc = DateTime.UtcNow;
        var session = module.GetSession(start.SessionId.Value);

        Assert.True(result.Success);
        Assert.Equal(start.SessionId.Value, result.SessionId);
        Assert.Equal(flowId, result.FlowId);
        Assert.Equal("Active", result.Status);
        Assert.Equal("cart", result.CurrentStep);
        Assert.Null(result.Error);
        Assert.Equal(SetCurrentStepOutcome.Changed, result.Outcome);
        var notification = Assert.IsType<StepChangedNotification>(result.Notification);
        Assert.Equal(start.SessionId.Value, notification.SessionId);
        Assert.Equal(flowId, notification.FlowId);
        Assert.Equal("cart", notification.CurrentStep);
        Assert.Null(notification.PreviousStep);
        Assert.Equal("operator-b", notification.ChangedBy);
        Assert.Equal("Operator", notification.ActorType);
        Assert.Equal("manual correction", notification.Reason);
        Assert.InRange(notification.OccurredAtUtc, beforeSetUtc, afterSetUtc);

        Assert.NotNull(session);
        Assert.Equal("cart", session.CurrentStep);
        Assert.Equal(2, session.Events.Count);

        var currentStepEvent = Assert.IsType<SessionCurrentStepSetEvent>(session.Events[1]);
        Assert.Equal(start.SessionId.Value, currentStepEvent.SessionId);
        Assert.Equal(flowId, currentStepEvent.FlowId);
        Assert.Equal("operator-b", currentStepEvent.ChangedBy);
        Assert.Equal("Operator", currentStepEvent.ActorType);
        Assert.Equal("manual correction", currentStepEvent.Reason);
        Assert.Null(currentStepEvent.PreviousStep);
        Assert.Equal("cart", currentStepEvent.CurrentStep);
        Assert.InRange(currentStepEvent.OccurredAtUtc, beforeSetUtc, afterSetUtc);
    }

    [Fact]
    public void SetCurrentStep_ShouldPreservePreviousStep_AndEventOrder_WhenTransitioningFromCartToPayment()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var first = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "cart", "operator-b", "Operator", "transition to cart"));
        var second = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId.Value, "payment", "operator-c", "System", null));
        var session = module.GetSession(start.SessionId.Value);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(SetCurrentStepOutcome.Changed, first.Outcome);
        Assert.Equal(SetCurrentStepOutcome.Changed, second.Outcome);
        Assert.Equal("cart", first.CurrentStep);
        Assert.Equal("payment", second.CurrentStep);
        var firstNotification = Assert.IsType<StepChangedNotification>(first.Notification);
        Assert.Null(firstNotification.PreviousStep);
        Assert.Equal("cart", firstNotification.CurrentStep);
        var secondNotification = Assert.IsType<StepChangedNotification>(second.Notification);
        Assert.Equal("cart", secondNotification.PreviousStep);
        Assert.Equal("payment", secondNotification.CurrentStep);

        Assert.NotNull(session);
        Assert.Equal(3, session.Events.Count);
        Assert.IsType<SessionStartedEvent>(session.Events[0]);

        var firstStepEvent = Assert.IsType<SessionCurrentStepSetEvent>(session.Events[1]);
        Assert.Null(firstStepEvent.PreviousStep);
        Assert.Equal("cart", firstStepEvent.CurrentStep);

        var secondStepEvent = Assert.IsType<SessionCurrentStepSetEvent>(session.Events[2]);
        Assert.Equal("cart", secondStepEvent.PreviousStep);
        Assert.Equal("payment", secondStepEvent.CurrentStep);
    }

    [Fact]
    public void SetCurrentStep_ShouldReject_WhenWorkflowHasMultipleStartSteps_AndAppendAuditEvent()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(CreateWorkflow(
            flowId,
            new[] { "cart", "payment", "review" },
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
            {
                ["cart"] = new[] { "payment" },
                ["payment"] = Array.Empty<string>(),
                ["review"] = Array.Empty<string>(),
            }));
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var result = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "cart", "operator-b", "System", null));
        var session = module.GetSession(start.SessionId.Value);

        Assert.False(result.Success);
        Assert.Equal(SetCurrentStepOutcome.Rejected, result.Outcome);
        Assert.Equal("Workflow definition is incomplete.", result.Error);
        Assert.Null(result.Notification);

        Assert.NotNull(session);
        Assert.Null(session.CurrentStep);
        Assert.Equal(2, session.Events.Count);

        var rejectedEvent = Assert.IsType<SessionCurrentStepRejectedWorkflowEvent>(session.Events[1]);
        Assert.Equal("cart", rejectedEvent.RequestedStep);
        Assert.Equal("Workflow definition is incomplete.", rejectedEvent.WorkflowError);
        Assert.Equal("WorkflowTransition", rejectedEvent.RejectionCategory);
    }

    [Fact]
    public void SetCurrentStep_ShouldRejectFirstAssignmentToDefinedNonStartStep_WhenCurrentStepIsNull()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var result = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "confirm", "operator-b", "System", null));
        var session = module.GetSession(start.SessionId.Value);

        Assert.False(result.Success);
        Assert.Equal(SetCurrentStepOutcome.Rejected, result.Outcome);
        Assert.Null(result.CurrentStep);
        Assert.Equal("TargetStep must be workflow start step.", result.Error);
        Assert.Null(result.Notification);

        Assert.NotNull(session);
        Assert.Null(session.CurrentStep);
        Assert.Equal(2, session.Events.Count);

        var rejectedEvent = Assert.IsType<SessionCurrentStepRejectedWorkflowEvent>(session.Events[1]);
        Assert.Null(rejectedEvent.CurrentStep);
        Assert.Equal("confirm", rejectedEvent.RequestedStep);
        Assert.Equal("Active", rejectedEvent.CurrentStatus);
        Assert.Equal("TargetStep must be workflow start step.", rejectedEvent.WorkflowError);
        Assert.Equal("WorkflowTransition", rejectedEvent.RejectionCategory);
    }

    [Fact]
    public void SetCurrentStep_ShouldReject_WhenSessionDoesNotExist_AndLeaveNoEvents()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var missing = module.SetCurrentStep(new SetCurrentStepCommand(Guid.NewGuid(), "payment", "operator-b", "Operator", null));
        var missingWithBlankReason = module.SetCurrentStep(new SetCurrentStepCommand(Guid.NewGuid(), "payment", "operator-c", "Operator", "   "));

        var session = module.GetSession(start.SessionId!.Value);

        Assert.False(missing.Success);
        Assert.Equal("Session not found.", missing.Error);
        Assert.Equal(SetCurrentStepOutcome.NotFound, missing.Outcome);
        Assert.Null(missing.Notification);

        Assert.False(missingWithBlankReason.Success);
        Assert.Equal("Session not found.", missingWithBlankReason.Error);
        Assert.Equal(SetCurrentStepOutcome.NotFound, missingWithBlankReason.Outcome);
        Assert.Null(missingWithBlankReason.Notification);

        Assert.NotNull(session);
        Assert.Equal("Active", session.Status);
        Assert.Null(session.CurrentStep);
        Assert.Single(session.Events);
        Assert.Empty(session.Events.OfType<SessionCurrentStepRejectedNotActiveEvent>());
    }

    [Fact]
    public void SetCurrentStep_ShouldReject_WhenSessionIdIsEmpty()
    {
        var module = new SessionModule();

        var result = module.SetCurrentStep(new SetCurrentStepCommand(Guid.Empty, "payment", "operator-b", "Operator", null));

        Assert.False(result.Success);
        Assert.Equal("SessionId is required.", result.Error);
        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Outcome);
        Assert.Null(result.Notification);
    }

    [Fact]
    public void SetCurrentStep_ShouldReject_WhenCurrentStepIsBlank()
    {
        var module = new SessionModule();
        var start = module.StartSession(new StartSessionCommand($"flow-{Guid.NewGuid():N}", "operator-a"));

        var result = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "   ", "operator-b", "Operator", null));

        Assert.False(result.Success);
        Assert.Equal("CurrentStep is required.", result.Error);
        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Outcome);
        Assert.Null(result.Notification);
    }

    [Fact]
    public void SetCurrentStep_ShouldReject_WhenSessionExistsButIsNotActive_AndAppendRejectedEvent()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));
        var currentStep = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, " cart ", "operator-b", "System", null));
        var end = module.EndSession(new EndSessionCommand(start.SessionId!.Value, "operator-a", "Operator", "done"));

        var beforeRetryUtc = DateTime.UtcNow;
        var ended = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId.Value, " payment ", " operator-b ", "oPeRaToR", "  operator retry  "));
        var afterRetryUtc = DateTime.UtcNow;
        var session = module.GetSession(start.SessionId.Value);

        Assert.True(currentStep.Success);
        Assert.True(end.Success);
        Assert.False(ended.Success);
        Assert.Equal(start.SessionId.Value, ended.SessionId);
        Assert.Equal(flowId, ended.FlowId);
        Assert.Equal("Ended", ended.Status);
        Assert.Equal("cart", ended.CurrentStep);
        Assert.Equal("Session is not active.", ended.Error);
        Assert.Equal(SetCurrentStepOutcome.NotActive, ended.Outcome);
        Assert.Null(ended.Notification);

        Assert.NotNull(session);
        Assert.Equal("Ended", session.Status);
        Assert.Equal("cart", session.CurrentStep);
        Assert.Equal(4, session.Events.Count);

        var rejectedEvent = Assert.IsType<SessionCurrentStepRejectedNotActiveEvent>(session.Events[3]);
        Assert.Equal(start.SessionId.Value, rejectedEvent.SessionId);
        Assert.Equal(flowId, rejectedEvent.FlowId);
        Assert.Equal("cart", rejectedEvent.CurrentStep);
        Assert.Equal("payment", rejectedEvent.RequestedStep);
        Assert.Equal("operator-b", rejectedEvent.ChangedBy);
        Assert.Equal("Operator", rejectedEvent.ActorType);
        Assert.Equal("operator retry", rejectedEvent.Reason);
        Assert.Equal("Ended", rejectedEvent.CurrentStatus);
        Assert.True(session.EndedAtUtc.HasValue);
        Assert.InRange(rejectedEvent.OccurredAtUtc, session.EndedAtUtc.Value, afterRetryUtc);
        Assert.InRange(rejectedEvent.OccurredAtUtc, beforeRetryUtc, afterRetryUtc);
        Assert.Single(session.Events.OfType<SessionCurrentStepSetEvent>());
    }

    [Fact]
    public void SetCurrentStep_ShouldReject_WhenEndedSessionOperatorReasonIsMissing_AndAppendRejectedEvent()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        Assert.True(module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "cart", "operator-b", "System", null)).Success);
        Assert.True(module.EndSession(new EndSessionCommand(start.SessionId.Value, "operator-a", "Operator", "done")).Success);

        var nullReasonResult = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId.Value, "review", "operator-c", "Operator", null));
        var blankReasonResult = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId.Value, "confirm", "operator-d", "Operator", "   "));
        var session = module.GetSession(start.SessionId.Value);

        Assert.False(nullReasonResult.Success);
        Assert.Equal("Reason is required for operator step change.", nullReasonResult.Error);
        Assert.Equal("Ended", nullReasonResult.Status);
        Assert.Equal(SetCurrentStepOutcome.NotActive, nullReasonResult.Outcome);
        Assert.Null(nullReasonResult.Notification);

        Assert.False(blankReasonResult.Success);
        Assert.Equal("Reason is required for operator step change.", blankReasonResult.Error);
        Assert.Equal("Ended", blankReasonResult.Status);
        Assert.Equal(SetCurrentStepOutcome.NotActive, blankReasonResult.Outcome);
        Assert.Null(blankReasonResult.Notification);

        Assert.NotNull(session);
        Assert.Equal("Ended", session.Status);
        Assert.Equal("cart", session.CurrentStep);
        Assert.Equal(5, session.Events.Count);

        var rejectedEvents = session.Events.OfType<SessionCurrentStepRejectedNotActiveEvent>().ToArray();
        Assert.Equal(2, rejectedEvents.Length);
        Assert.Equal("review", rejectedEvents[0].RequestedStep);
        Assert.Null(rejectedEvents[0].Reason);
        Assert.Equal("confirm", rejectedEvents[1].RequestedStep);
        Assert.Null(rejectedEvents[1].Reason);
    }

    [Fact]
    public void SetCurrentStep_ShouldAppendOneRejectedEventPerRetry_WhenSessionIsEnded()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        Assert.True(module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "cart", "operator-b", "Operator", "initial step")).Success);
        Assert.True(module.EndSession(new EndSessionCommand(start.SessionId.Value, "operator-a", "Operator", "done")).Success);

        var firstRetry = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId.Value, "review", "operator-c", "System", "retry-1"));
        var secondRetry = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId.Value, "confirm", "operator-d", "System", "retry-2"));
        var session = module.GetSession(start.SessionId.Value);

        Assert.False(firstRetry.Success);
        Assert.False(secondRetry.Success);
        Assert.Equal(SetCurrentStepOutcome.NotActive, firstRetry.Outcome);
        Assert.Equal(SetCurrentStepOutcome.NotActive, secondRetry.Outcome);
        Assert.Equal(firstRetry.Status, secondRetry.Status);
        Assert.Equal(firstRetry.CurrentStep, secondRetry.CurrentStep);
        Assert.Null(firstRetry.Notification);
        Assert.Null(secondRetry.Notification);

        Assert.NotNull(session);
        Assert.Equal("Ended", session.Status);
        Assert.Equal("cart", session.CurrentStep);
        Assert.NotNull(session.EndedAtUtc);
        Assert.Equal(5, session.Events.Count);
        Assert.Single(session.Events.OfType<SessionCurrentStepSetEvent>());
        Assert.Equal(2, session.Events.OfType<SessionCurrentStepRejectedNotActiveEvent>().Count());

        Assert.IsType<SessionStartedEvent>(session.Events[0]);
        Assert.IsType<SessionCurrentStepSetEvent>(session.Events[1]);
        Assert.IsType<SessionEndedEvent>(session.Events[2]);

        var firstRejectedEvent = Assert.IsType<SessionCurrentStepRejectedNotActiveEvent>(session.Events[3]);
        var secondRejectedEvent = Assert.IsType<SessionCurrentStepRejectedNotActiveEvent>(session.Events[4]);
        Assert.Equal("cart", firstRejectedEvent.CurrentStep);
        Assert.Equal("review", firstRejectedEvent.RequestedStep);
        Assert.Equal("cart", secondRejectedEvent.CurrentStep);
        Assert.Equal("confirm", secondRejectedEvent.RequestedStep);
        Assert.Equal("Ended", firstRejectedEvent.CurrentStatus);
        Assert.Equal("Ended", secondRejectedEvent.CurrentStatus);
        Assert.True(firstRejectedEvent.OccurredAtUtc >= session.EndedAtUtc.Value);
        Assert.True(secondRejectedEvent.OccurredAtUtc >= session.EndedAtUtc.Value);
        Assert.True(secondRejectedEvent.OccurredAtUtc >= firstRejectedEvent.OccurredAtUtc);
    }

    [Fact]
    public void SetCurrentStep_ShouldClampRejectedNotActiveTimestamp_ToForcedEndedAtUtc()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        Assert.True(module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "cart", "operator-b", "Operator", "initial step")).Success);
        Assert.True(module.EndSession(new EndSessionCommand(start.SessionId.Value, "operator-a", "Operator", "done")).Success);

        var forcedEndedAtUtc = DateTime.UtcNow.AddHours(1);
        SetSessionEndedAtUtc(start.SessionId.Value, forcedEndedAtUtc);

        var result = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId.Value, " payment ", " operator-b ", "oPeRaToR", "  retry  "));
        var session = module.GetSession(start.SessionId.Value);

        Assert.False(result.Success);
        Assert.Equal(SetCurrentStepOutcome.NotActive, result.Outcome);
        Assert.NotNull(session);
        Assert.Equal(forcedEndedAtUtc, session.EndedAtUtc);
        var endedAtUtc = Assert.IsType<DateTime>(session.EndedAtUtc);

        var rejectedEvent = Assert.IsType<SessionCurrentStepRejectedNotActiveEvent>(session.Events[^1]);
        Assert.Equal("cart", rejectedEvent.CurrentStep);
        Assert.Equal("payment", rejectedEvent.RequestedStep);
        Assert.Equal("operator-b", rejectedEvent.ChangedBy);
        Assert.Equal("Operator", rejectedEvent.ActorType);
        Assert.Equal("retry", rejectedEvent.Reason);
        Assert.Equal("Ended", rejectedEvent.CurrentStatus);
        Assert.Equal(forcedEndedAtUtc, rejectedEvent.OccurredAtUtc);
        Assert.True(rejectedEvent.OccurredAtUtc >= endedAtUtc);
    }

    [Fact]
    public void SetCurrentStep_ShouldReturnUnchanged_AndAppendSessionCurrentStepUnchangedEvent_WhenSameStepRequested()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var first = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "cart", "operator-b", "System", null));
        var beforeSecondSetUtc = DateTime.UtcNow;
        var second = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId.Value, " cart ", "operator-c", "sYsTeM", "   "));
        var afterSecondSetUtc = DateTime.UtcNow;

        var session = module.GetSession(start.SessionId.Value);

        Assert.True(first.Success);
        Assert.Equal(SetCurrentStepOutcome.Changed, first.Outcome);
        Assert.NotNull(first.Notification);
        Assert.True(second.Success);
        Assert.Equal(SetCurrentStepOutcome.Unchanged, second.Outcome);
        Assert.Equal("cart", second.CurrentStep);
        Assert.Null(second.Error);
        Assert.Null(second.Notification);

        Assert.NotNull(session);
        Assert.Equal("cart", session.CurrentStep);
        Assert.Equal(3, session.Events.Count);
        Assert.IsType<SessionCurrentStepSetEvent>(session.Events[1]);

        var unchangedEvent = Assert.IsType<SessionCurrentStepUnchangedEvent>(session.Events[2]);
        Assert.Equal(start.SessionId.Value, unchangedEvent.SessionId);
        Assert.Equal(flowId, unchangedEvent.FlowId);
        Assert.Equal("cart", unchangedEvent.CurrentStep);
        Assert.Equal("cart", unchangedEvent.RequestedStep);
        Assert.Equal("operator-c", unchangedEvent.ChangedBy);
        Assert.Equal("System", unchangedEvent.ActorType);
        Assert.Null(unchangedEvent.Reason);
        Assert.Equal("Unchanged", unchangedEvent.Outcome);
        Assert.InRange(unchangedEvent.OccurredAtUtc, beforeSecondSetUtc, afterSecondSetUtc);
    }

    [Fact]
    public void SetCurrentStep_ShouldReject_WhenWorkflowTargetStepIsUnknown_AndAppendAuditEvent()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var first = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "cart", "operator-b", "System", null));
        var second = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId.Value, "unmapped-step", "operator-c", "System", "retry"));
        var session = module.GetSession(start.SessionId.Value);

        Assert.True(first.Success);
        Assert.Equal(SetCurrentStepOutcome.Changed, first.Outcome);
        Assert.Equal("cart", first.CurrentStep);
        Assert.NotNull(first.Notification);

        Assert.False(second.Success);
        Assert.Equal(SetCurrentStepOutcome.Rejected, second.Outcome);
        Assert.Equal("cart", second.CurrentStep);
        Assert.Equal("TargetStep is not defined.", second.Error);
        Assert.Null(second.Notification);

        Assert.NotNull(session);
        Assert.Equal("cart", session.CurrentStep);
        Assert.Equal(3, session.Events.Count);
        Assert.IsType<SessionStartedEvent>(session.Events[0]);

        var firstStepEvent = Assert.IsType<SessionCurrentStepSetEvent>(session.Events[1]);
        Assert.Equal("cart", firstStepEvent.CurrentStep);

        var rejectedEvent = Assert.IsType<SessionCurrentStepRejectedWorkflowEvent>(session.Events[2]);
        Assert.Equal("cart", rejectedEvent.CurrentStep);
        Assert.Equal("unmapped-step", rejectedEvent.RequestedStep);
        Assert.Equal("operator-c", rejectedEvent.ChangedBy);
        Assert.Equal("System", rejectedEvent.ActorType);
        Assert.Equal("retry", rejectedEvent.Reason);
        Assert.Equal("Active", rejectedEvent.CurrentStatus);
        Assert.Equal("TargetStep is not defined.", rejectedEvent.WorkflowError);
        Assert.Equal("Rejected", rejectedEvent.Outcome);
        Assert.Equal("WorkflowTransition", rejectedEvent.RejectionCategory);
    }

    [Fact]
    public void SetCurrentStep_ShouldReject_WhenWorkflowTransitionIsKnownButNotAllowed_AndAppendAuditEvent()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var first = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "cart", "operator-b", "System", null));
        var second = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId.Value, "review", "operator-c", "System", "backward transition"));
        var session = module.GetSession(start.SessionId.Value);

        Assert.True(first.Success);
        Assert.Equal(SetCurrentStepOutcome.Changed, first.Outcome);

        Assert.False(second.Success);
        Assert.Equal(SetCurrentStepOutcome.Rejected, second.Outcome);
        Assert.Equal("cart", second.CurrentStep);
        Assert.Equal("Transition is not allowed.", second.Error);
        Assert.Null(second.Notification);

        Assert.NotNull(session);
        Assert.Equal("cart", session.CurrentStep);
        Assert.Equal(3, session.Events.Count);
        Assert.Single(session.Events.OfType<SessionCurrentStepSetEvent>());
        Assert.Empty(session.Events.OfType<SessionCurrentStepUnchangedEvent>());

        var rejectedEvent = Assert.IsType<SessionCurrentStepRejectedWorkflowEvent>(session.Events[2]);
        Assert.Equal("cart", rejectedEvent.CurrentStep);
        Assert.Equal("review", rejectedEvent.RequestedStep);
        Assert.Equal("Active", rejectedEvent.CurrentStatus);
        Assert.Equal("Transition is not allowed.", rejectedEvent.WorkflowError);
        Assert.Equal("Rejected", rejectedEvent.Outcome);
        Assert.Equal("WorkflowTransition", rejectedEvent.RejectionCategory);
    }

    [Fact]
    public void SetCurrentStep_ShouldUseFlowSpecificWorkflowDefinition()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(CreateWorkflow(
            flowId,
            new[] { "scan", "authorize", "capture" },
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
            {
                ["scan"] = new[] { "authorize" },
                ["authorize"] = new[] { "capture" },
                ["capture"] = Array.Empty<string>(),
            }));
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var first = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "scan", "operator-b", "System", null));
        var second = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId.Value, "payment", "operator-c", "System", "wrong flow step"));
        var session = module.GetSession(start.SessionId.Value);

        Assert.True(first.Success);
        Assert.Equal("scan", first.CurrentStep);
        Assert.False(second.Success);
        Assert.Equal(SetCurrentStepOutcome.Rejected, second.Outcome);
        Assert.Equal("scan", second.CurrentStep);
        Assert.Equal("TargetStep is not defined.", second.Error);

        Assert.NotNull(session);
        Assert.Equal("scan", session.CurrentStep);
        Assert.Equal(3, session.Events.Count);
        var rejectedEvent = Assert.IsType<SessionCurrentStepRejectedWorkflowEvent>(session.Events[2]);
        Assert.Equal("scan", rejectedEvent.CurrentStep);
        Assert.Equal("payment", rejectedEvent.RequestedStep);
        Assert.Equal("TargetStep is not defined.", rejectedEvent.WorkflowError);
        Assert.Equal("WorkflowTransition", rejectedEvent.RejectionCategory);
    }

    [Fact]
    public void SetCurrentStep_ShouldReject_WhenCurrentAndTargetAreSameUnknownStep_AndNotRecordNoOp()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));
        SetSessionCurrentStep(start.SessionId!.Value, "legacy-unknown");

        var result = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId.Value, " legacy-unknown ", "operator-b", "System", null));
        var session = module.GetSession(start.SessionId.Value);

        Assert.False(result.Success);
        Assert.Equal(SetCurrentStepOutcome.Rejected, result.Outcome);
        Assert.Equal("legacy-unknown", result.CurrentStep);
        Assert.Equal("CurrentStep is not defined.", result.Error);
        Assert.Null(result.Notification);

        Assert.NotNull(session);
        Assert.Equal("legacy-unknown", session.CurrentStep);
        Assert.Equal(2, session.Events.Count);
        Assert.Empty(session.Events.OfType<SessionCurrentStepUnchangedEvent>());

        var rejectedEvent = Assert.IsType<SessionCurrentStepRejectedWorkflowEvent>(session.Events[1]);
        Assert.Equal("legacy-unknown", rejectedEvent.CurrentStep);
        Assert.Equal("legacy-unknown", rejectedEvent.RequestedStep);
        Assert.Equal("Active", rejectedEvent.CurrentStatus);
        Assert.Equal("CurrentStep is not defined.", rejectedEvent.WorkflowError);
        Assert.Equal("WorkflowTransition", rejectedEvent.RejectionCategory);
    }

    [Fact]
    public void SetCurrentStep_ShouldReject_WhenChangedByIsMissing()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var result = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "payment", " ", "Operator", null));

        Assert.False(result.Success);
        Assert.Equal("ChangedBy is required.", result.Error);
        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Outcome);
        Assert.Null(result.Notification);
    }

    [Fact]
    public void SetCurrentStep_ShouldReject_WhenActorTypeIsInvalid()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var result = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "payment", "operator-b", "auditor", null));

        Assert.False(result.Success);
        Assert.Equal("ActorType is invalid.", result.Error);
        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Outcome);
        Assert.Null(result.Notification);

        var session = module.GetSession(start.SessionId!.Value);

        Assert.NotNull(session);
        Assert.Null(session.CurrentStep);
        Assert.Single(session.Events);
        Assert.IsType<SessionStartedEvent>(session.Events[0]);
        Assert.Empty(session.Events.OfType<SessionCurrentStepSetEvent>());
        Assert.Empty(session.Events.OfType<SessionCurrentStepRejectedNotActiveEvent>());
    }

    [Fact]
    public void SetCurrentStep_ShouldReject_WhenOperatorReasonIsMissing()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var missingReason = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "payment", "operator-b", "Operator", null));
        var blankReason = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId.Value, "review", "operator-c", "oPeRaToR", "   "));

        Assert.False(missingReason.Success);
        Assert.Equal("Reason is required for operator step change.", missingReason.Error);
        Assert.Equal("Rejected", missingReason.Status);
        Assert.Null(missingReason.Outcome);
        Assert.Null(missingReason.Notification);

        Assert.False(blankReason.Success);
        Assert.Equal("Reason is required for operator step change.", blankReason.Error);
        Assert.Equal("Rejected", blankReason.Status);
        Assert.Null(blankReason.Outcome);
        Assert.Null(blankReason.Notification);

        var session = module.GetSession(start.SessionId!.Value);

        Assert.NotNull(session);
        Assert.Null(session.CurrentStep);
        Assert.Single(session.Events);
        Assert.IsType<SessionStartedEvent>(session.Events[0]);
        Assert.Empty(session.Events.OfType<SessionCurrentStepSetEvent>());
        Assert.Empty(session.Events.OfType<SessionCurrentStepRejectedNotActiveEvent>());
    }

    [Fact]
    public void SetCurrentStep_ShouldReject_WhenActorTypeIsBlank()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var result = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "payment", "operator-b", "   ", null));
        var session = module.GetSession(start.SessionId.Value);

        Assert.False(result.Success);
        Assert.Equal("ActorType is required.", result.Error);
        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Outcome);
        Assert.Null(result.Notification);

        Assert.NotNull(session);
        Assert.Null(session.CurrentStep);
        Assert.Single(session.Events);
        Assert.IsType<SessionStartedEvent>(session.Events[0]);
        Assert.Empty(session.Events.OfType<SessionCurrentStepSetEvent>());
        Assert.Empty(session.Events.OfType<SessionCurrentStepRejectedNotActiveEvent>());
    }

    [Fact]
    public void SetCurrentStep_ShouldReject_WhenChangedByIsMissing_AndLeaveSessionStateUnchanged()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var result = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "payment", " ", "Operator", null));
        var session = module.GetSession(start.SessionId.Value);

        Assert.False(result.Success);
        Assert.Equal("ChangedBy is required.", result.Error);
        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Outcome);
        Assert.Null(result.Notification);

        Assert.NotNull(session);
        Assert.Null(session.CurrentStep);
        Assert.Single(session.Events);
        Assert.IsType<SessionStartedEvent>(session.Events[0]);
        Assert.Empty(session.Events.OfType<SessionCurrentStepSetEvent>());
        Assert.Empty(session.Events.OfType<SessionCurrentStepRejectedNotActiveEvent>());
    }

    [Fact]
    public void SetCurrentStep_DefaultConstructorShouldAllowStepChangeWithoutWorkflowDefinition()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = new SessionModule();
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var result = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "payment", "operator-b", "System", null));
        var session = module.GetSession(start.SessionId.Value);

        Assert.True(result.Success);
        Assert.Equal(SetCurrentStepOutcome.Changed, result.Outcome);
        Assert.Null(result.Error);
        Assert.Equal("payment", result.CurrentStep);
        Assert.NotNull(result.Notification);

        Assert.NotNull(session);
        Assert.Equal("payment", session.CurrentStep);
        Assert.Equal(2, session.Events.Count);
        Assert.IsType<SessionCurrentStepSetEvent>(session.Events[1]);
        Assert.Empty(session.Events.OfType<SessionCurrentStepRejectedWorkflowEvent>());
    }

    [Fact]
    public void SetCurrentStep_WithWorkflowProviderShouldRejectMissingWorkflowDefinition_AndAppendWorkflowAuditEvent()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule();
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var result = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "payment", "operator-b", "System", null));
        var session = module.GetSession(start.SessionId.Value);

        Assert.False(result.Success);
        Assert.Equal(SetCurrentStepOutcome.Rejected, result.Outcome);
        Assert.Equal("Workflow definition not found.", result.Error);
        Assert.Null(result.CurrentStep);
        Assert.Null(result.Notification);

        Assert.NotNull(session);
        Assert.Null(session.CurrentStep);
        Assert.Equal(2, session.Events.Count);
        Assert.Empty(session.Events.OfType<SessionCurrentStepRejectedNotActiveEvent>());

        var rejectedEvent = Assert.IsType<SessionCurrentStepRejectedWorkflowEvent>(session.Events[1]);
        Assert.Null(rejectedEvent.CurrentStep);
        Assert.Equal("payment", rejectedEvent.RequestedStep);
        Assert.Equal("operator-b", rejectedEvent.ChangedBy);
        Assert.Equal("System", rejectedEvent.ActorType);
        Assert.Null(rejectedEvent.Reason);
        Assert.Equal("Active", rejectedEvent.CurrentStatus);
        Assert.Equal("Workflow definition not found.", rejectedEvent.WorkflowError);
        Assert.Equal("Rejected", rejectedEvent.Outcome);
        Assert.Equal("WorkflowTransition", rejectedEvent.RejectionCategory);
    }

    [Fact]
    public void SetCurrentStep_ShouldNormalizeReason_WhenNullOrWhitespaceIsProvided()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var nullReason = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "cart", "operator-b", "System", null));
        var emptyReason = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId.Value, "payment", "operator-c", "System", " "));
        var session = module.GetSession(start.SessionId.Value);

        Assert.True(nullReason.Success);
        Assert.True(emptyReason.Success);
        Assert.NotNull(session);

        var firstStepEvent = Assert.IsType<SessionCurrentStepSetEvent>(session.Events[1]);
        var secondStepEvent = Assert.IsType<SessionCurrentStepSetEvent>(session.Events[2]);
        Assert.Null(firstStepEvent.Reason);
        Assert.Null(secondStepEvent.Reason);
    }

    private static void SetSessionEndedAtUtc(Guid sessionId, DateTime endedAtUtc)
    {
        var sharedStore = typeof(SessionModule)
            .GetField("SharedStore", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        var sessionsField = sharedStore.GetType().GetField("_sessions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var sessions = (Dictionary<Guid, SessionRecord>)sessionsField.GetValue(sharedStore)!;
        var session = sessions[sessionId];

        typeof(SessionRecord)
            .GetField("<EndedAtUtc>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(session, endedAtUtc);
    }

    private static void SetSessionCurrentStep(Guid sessionId, string currentStep)
    {
        var sharedStore = typeof(SessionModule)
            .GetField("SharedStore", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        var sessionsField = sharedStore.GetType().GetField("_sessions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var sessions = (Dictionary<Guid, SessionRecord>)sessionsField.GetValue(sharedStore)!;
        var session = sessions[sessionId];

        typeof(SessionRecord)
            .GetField("<CurrentStep>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(session, currentStep);
    }
}
