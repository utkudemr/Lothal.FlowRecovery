using Lothal.FlowRecovery.Modules.Session;

namespace Lothal.FlowRecovery.Modules.Session.Tests;

public sealed class SetCurrentStepTests
{
    [Fact]
    public void SetCurrentStep_ShouldSetActiveSession_AndAppendSessionCurrentStepSetEvent()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));
        var beforeSetUtc = DateTime.UtcNow;

        var result = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, " payment ", "operator-b", "oPeRaToR", "  manual correction  "));

        var afterSetUtc = DateTime.UtcNow;
        var session = module.GetSession(start.SessionId.Value);

        Assert.True(result.Success);
        Assert.Equal(start.SessionId.Value, result.SessionId);
        Assert.Equal(flowId, result.FlowId);
        Assert.Equal("Active", result.Status);
        Assert.Equal("payment", result.CurrentStep);
        Assert.Null(result.Error);
        Assert.Equal(SetCurrentStepOutcome.Changed, result.Outcome);
        var notification = Assert.IsType<StepChangedNotification>(result.Notification);
        Assert.Equal(start.SessionId.Value, notification.SessionId);
        Assert.Equal(flowId, notification.FlowId);
        Assert.Equal("payment", notification.CurrentStep);
        Assert.Null(notification.PreviousStep);
        Assert.Equal("operator-b", notification.ChangedBy);
        Assert.Equal("Operator", notification.ActorType);
        Assert.Equal("manual correction", notification.Reason);
        Assert.InRange(notification.OccurredAtUtc, beforeSetUtc, afterSetUtc);

        Assert.NotNull(session);
        Assert.Equal("payment", session.CurrentStep);
        Assert.Equal(2, session.Events.Count);

        var currentStepEvent = Assert.IsType<SessionCurrentStepSetEvent>(session.Events[1]);
        Assert.Equal(start.SessionId.Value, currentStepEvent.SessionId);
        Assert.Equal(flowId, currentStepEvent.FlowId);
        Assert.Equal("operator-b", currentStepEvent.ChangedBy);
        Assert.Equal("Operator", currentStepEvent.ActorType);
        Assert.Equal("manual correction", currentStepEvent.Reason);
        Assert.Null(currentStepEvent.PreviousStep);
        Assert.Equal("payment", currentStepEvent.CurrentStep);
        Assert.InRange(currentStepEvent.OccurredAtUtc, beforeSetUtc, afterSetUtc);
    }

    [Fact]
    public void SetCurrentStep_ShouldPreservePreviousStep_AndEventOrder_WhenTransitioningFromAToB()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var first = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "A", "operator-b", "Operator", null));
        var second = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId.Value, "B", "operator-c", "System", null));
        var session = module.GetSession(start.SessionId.Value);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(SetCurrentStepOutcome.Changed, first.Outcome);
        Assert.Equal(SetCurrentStepOutcome.Changed, second.Outcome);
        Assert.Equal("A", first.CurrentStep);
        Assert.Equal("B", second.CurrentStep);
        var firstNotification = Assert.IsType<StepChangedNotification>(first.Notification);
        Assert.Null(firstNotification.PreviousStep);
        Assert.Equal("A", firstNotification.CurrentStep);
        var secondNotification = Assert.IsType<StepChangedNotification>(second.Notification);
        Assert.Equal("A", secondNotification.PreviousStep);
        Assert.Equal("B", secondNotification.CurrentStep);

        Assert.NotNull(session);
        Assert.Equal(3, session.Events.Count);
        Assert.IsType<SessionStartedEvent>(session.Events[0]);

        var firstStepEvent = Assert.IsType<SessionCurrentStepSetEvent>(session.Events[1]);
        Assert.Null(firstStepEvent.PreviousStep);
        Assert.Equal("A", firstStepEvent.CurrentStep);

        var secondStepEvent = Assert.IsType<SessionCurrentStepSetEvent>(session.Events[2]);
        Assert.Equal("A", secondStepEvent.PreviousStep);
        Assert.Equal("B", secondStepEvent.CurrentStep);
    }

    [Fact]
    public void SetCurrentStep_ShouldReject_WhenSessionDoesNotExistOrSessionIsNotActive()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));
        var end = module.EndSession(new EndSessionCommand(start.SessionId!.Value, "operator-a", "Operator", "done"));

        var missing = module.SetCurrentStep(new SetCurrentStepCommand(Guid.NewGuid(), "payment", "operator-b", "Operator", null));
        var ended = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId.Value, "payment", "operator-b", "Operator", null));

        var session = module.GetSession(start.SessionId.Value);

        Assert.True(end.Success);
        Assert.False(missing.Success);
        Assert.Equal("Session not found.", missing.Error);
        Assert.Equal(SetCurrentStepOutcome.NotFound, missing.Outcome);
        Assert.Null(missing.Notification);

        Assert.False(ended.Success);
        Assert.Equal(start.SessionId.Value, ended.SessionId);
        Assert.Equal(flowId, ended.FlowId);
        Assert.Equal("Ended", ended.Status);
        Assert.Null(ended.CurrentStep);
        Assert.Equal("Session is not active.", ended.Error);
        Assert.Equal(SetCurrentStepOutcome.NotActive, ended.Outcome);
        Assert.Null(ended.Notification);

        Assert.NotNull(session);
        Assert.Equal("Ended", session.Status);
        Assert.Null(session.CurrentStep);
        Assert.Equal(2, session.Events.Count);
        Assert.Empty(session.Events.OfType<SessionCurrentStepSetEvent>());
    }

    [Fact]
    public void SetCurrentStep_ShouldReturnUnchanged_AndAppendSessionCurrentStepUnchangedEvent_WhenSameStepRequested()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var first = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "payment", "operator-b", "System", null));
        var beforeSecondSetUtc = DateTime.UtcNow;
        var second = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId.Value, " payment ", "operator-c", "sYsTeM", "   "));
        var afterSecondSetUtc = DateTime.UtcNow;

        var session = module.GetSession(start.SessionId.Value);

        Assert.True(first.Success);
        Assert.Equal(SetCurrentStepOutcome.Changed, first.Outcome);
        Assert.NotNull(first.Notification);
        Assert.True(second.Success);
        Assert.Equal(SetCurrentStepOutcome.Unchanged, second.Outcome);
        Assert.Equal("payment", second.CurrentStep);
        Assert.Null(second.Error);
        Assert.Null(second.Notification);

        Assert.NotNull(session);
        Assert.Equal("payment", session.CurrentStep);
        Assert.Equal(3, session.Events.Count);
        Assert.IsType<SessionCurrentStepSetEvent>(session.Events[1]);

        var unchangedEvent = Assert.IsType<SessionCurrentStepUnchangedEvent>(session.Events[2]);
        Assert.Equal(start.SessionId.Value, unchangedEvent.SessionId);
        Assert.Equal(flowId, unchangedEvent.FlowId);
        Assert.Equal("payment", unchangedEvent.CurrentStep);
        Assert.Equal("payment", unchangedEvent.RequestedStep);
        Assert.Equal("operator-c", unchangedEvent.ChangedBy);
        Assert.Equal("System", unchangedEvent.ActorType);
        Assert.Null(unchangedEvent.Reason);
        Assert.Equal("Unchanged", unchangedEvent.Outcome);
        Assert.InRange(unchangedEvent.OccurredAtUtc, beforeSecondSetUtc, afterSecondSetUtc);
    }

    [Fact]
    public void SetCurrentStep_ShouldReject_WhenChangedByIsMissing()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
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
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var result = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "payment", "operator-b", "auditor", null));

        Assert.False(result.Success);
        Assert.Equal("ActorType is invalid.", result.Error);
        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Outcome);
        Assert.Null(result.Notification);
    }

    [Fact]
    public void SetCurrentStep_ShouldNormalizeReason_WhenNullOrWhitespaceIsProvided()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var start = module.StartSession(new StartSessionCommand(flowId, "operator-a"));

        var nullReason = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId!.Value, "payment", "operator-b", "System", null));
        var emptyReason = module.SetCurrentStep(new SetCurrentStepCommand(start.SessionId.Value, "review", "operator-c", "System", " "));
        var session = module.GetSession(start.SessionId.Value);

        Assert.True(nullReason.Success);
        Assert.True(emptyReason.Success);
        Assert.NotNull(session);

        var firstStepEvent = Assert.IsType<SessionCurrentStepSetEvent>(session.Events[1]);
        var secondStepEvent = Assert.IsType<SessionCurrentStepSetEvent>(session.Events[2]);
        Assert.Null(firstStepEvent.Reason);
        Assert.Null(secondStepEvent.Reason);
    }
}
