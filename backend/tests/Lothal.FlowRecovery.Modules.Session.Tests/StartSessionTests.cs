using Lothal.FlowRecovery.Modules.Session;
using Lothal.FlowRecovery.Modules.Workflow;
using static Lothal.FlowRecovery.Modules.Session.Tests.SessionWorkflowTestDefinitions;

namespace Lothal.FlowRecovery.Modules.Session.Tests;

public sealed class StartSessionTests
{
    [Fact]
    public void StartSession_ShouldReject_WhenFlowIdIsMissing()
    {
        var module = new SessionModule();

        var result = module.StartSession(new StartSessionCommand("   ", "operator-a"));

        Assert.False(result.Success);
        Assert.Equal("FlowId is required.", result.Error);
        Assert.Null(result.SessionId);
        Assert.Null(result.StartStep);
        Assert.Null(result.Outcome);
        Assert.Null(result.Notification);
    }

    [Fact]
    public void StartSession_ShouldReject_WhenStartedByIsMissing()
    {
        var module = new SessionModule();

        var flowId = $"flow-{Guid.NewGuid():N}";
        var result = module.StartSession(new StartSessionCommand(flowId, " "));

        Assert.False(result.Success);
        Assert.Equal("StartedBy is required.", result.Error);
        Assert.Equal(flowId, result.FlowId);
        Assert.Null(result.StartStep);
        Assert.Null(result.Outcome);
        Assert.Null(result.Notification);
    }

    [Fact]
    public void StartSession_ShouldRejectDuplicateActiveSession_ForSameFlowId()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";

        var first = module.StartSession(new StartSessionCommand(flowId, "operator-a"));
        var second = module.StartSession(new StartSessionCommand(flowId, "operator-b"));
        var session = module.GetSession(first.SessionId!.Value);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal(first.SessionId, second.SessionId);
        Assert.Equal(first.FlowId, second.FlowId);
        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.StartedAtUtc, second.StartedAtUtc);
        Assert.Null(second.StartStep);
        Assert.Equal("Active session already exists.", second.Error);
        Assert.Equal(StartSessionOutcome.DuplicateActiveSession, second.Outcome);
        Assert.Null(second.Notification);
        Assert.NotNull(session);
        Assert.Equal("Active", session.Status);
        Assert.Equal(2, session.Events.Count);
        Assert.IsType<SessionStartedEvent>(session.Events[0]);

        var duplicateAuditEvent = Assert.IsType<SessionStartDuplicateAuditEvent>(session.Events[1]);
        Assert.Equal(first.SessionId.Value, duplicateAuditEvent.SessionId);
        Assert.Equal(flowId, duplicateAuditEvent.FlowId);
        Assert.Equal("operator-b", duplicateAuditEvent.RequestedBy);
        Assert.Equal("Active", duplicateAuditEvent.CurrentStatus);
        Assert.True(duplicateAuditEvent.OccurredAtUtc >= first.StartedAtUtc);
    }

    [Fact]
    public void StartSession_ShouldPreserveStartStep_OnDuplicate_WhenWorkflowDefinitionIsUnavailableAfterFirstStart()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var workflowDefinition = CreateCheckoutWorkflow(flowId);
        var workflowDefinitions = new MutableWorkflowDefinitionProvider(workflowDefinition);
        var module = new SessionModule(workflowDefinitions);

        var first = module.StartSession(new StartSessionCommand(flowId, "operator-a"));
        workflowDefinitions.Clear();
        var second = module.StartSession(new StartSessionCommand(flowId, "operator-b"));
        var session = module.GetSession(first.SessionId!.Value);

        Assert.True(first.Success);
        Assert.Equal("cart", first.StartStep);
        var firstNotification = Assert.IsType<SessionStartedNotification>(first.Notification);
        Assert.Equal("cart", firstNotification.StartStep);
        Assert.True(second.SessionId.HasValue);
        Assert.Equal(first.SessionId, second.SessionId);
        Assert.False(second.Success);
        Assert.Equal("cart", second.StartStep);
        Assert.Equal(StartSessionOutcome.DuplicateActiveSession, second.Outcome);
        Assert.Null(second.Notification);
        Assert.NotNull(session);
        Assert.Equal(2, session.Events.Count);
        Assert.IsType<SessionStartedEvent>(session.Events[0]);

        var duplicateAuditEvent = Assert.IsType<SessionStartDuplicateAuditEvent>(session.Events[1]);
        Assert.Equal(first.SessionId.Value, duplicateAuditEvent.SessionId);
        Assert.Equal(flowId, duplicateAuditEvent.FlowId);
        Assert.Equal("operator-b", duplicateAuditEvent.RequestedBy);
        Assert.Equal("Active", duplicateAuditEvent.CurrentStatus);
        Assert.True(duplicateAuditEvent.OccurredAtUtc >= first.StartedAtUtc);
    }

    [Fact]
    public void StartSession_ShouldCreateSession_WhenRequestIsValid()
    {
        var module = new SessionModule();
        var flowId = $"flow-{Guid.NewGuid():N}";

        var result = module.StartSession(new StartSessionCommand(flowId, "operator-a"));
        var session = module.GetSession(result.SessionId!.Value);

        Assert.True(result.Success);
        Assert.NotNull(result.SessionId);
        Assert.Equal(flowId, result.FlowId);
        Assert.Equal("Active", result.Status);
        Assert.NotNull(result.StartedAtUtc);
        Assert.Null(result.StartStep);
        Assert.Null(result.Error);
        Assert.Equal(StartSessionOutcome.Started, result.Outcome);
        var notification = Assert.IsType<SessionStartedNotification>(result.Notification);
        Assert.Equal(result.SessionId, notification.SessionId);
        Assert.Equal(flowId, notification.FlowId);
        Assert.Equal("operator-a", notification.StartedBy);
        Assert.Equal(result.StartedAtUtc, notification.OccurredAtUtc);
        Assert.Null(notification.StartStep);
        Assert.NotNull(session);
        var startedEvent = Assert.IsType<SessionStartedEvent>(session.Events[0]);
        Assert.Equal(result.SessionId, startedEvent.SessionId);
        Assert.Equal(flowId, startedEvent.FlowId);
        Assert.Equal("operator-a", startedEvent.StartedBy);
        Assert.Equal(result.StartedAtUtc, startedEvent.OccurredAtUtc);
    }

    [Fact]
    public void StartSession_ShouldReturnStartStep_WhenWorkflowDefinitionIsAvailable()
    {
        var flowId = $"flow-{Guid.NewGuid():N}";
        var module = CreateModule(flowId);

        var result = module.StartSession(new StartSessionCommand(flowId, "operator-a"));
        var session = module.GetSession(result.SessionId!.Value);

        Assert.True(result.Success);
        Assert.Equal("cart", result.StartStep);
        var notification = Assert.IsType<SessionStartedNotification>(result.Notification);
        Assert.Equal(result.StartStep, notification.StartStep);
        Assert.NotNull(session);
        Assert.Null(session.CurrentStep);
        Assert.Single(session.Events);
        Assert.IsType<SessionStartedEvent>(session.Events[0]);
    }

    [Fact]
    public void StartSession_ShouldLeaveStartStepNull_WhenWorkflowDefinitionIsMissing()
    {
        var module = CreateModule();
        var flowId = $"flow-{Guid.NewGuid():N}";

        var result = module.StartSession(new StartSessionCommand(flowId, "operator-a"));
        var session = module.GetSession(result.SessionId!.Value);

        Assert.True(result.Success);
        Assert.Null(result.StartStep);
        var notification = Assert.IsType<SessionStartedNotification>(result.Notification);
        Assert.Null(notification.StartStep);
        Assert.NotNull(session);
        Assert.Null(session.CurrentStep);
        Assert.Single(session.Events);
        Assert.IsType<SessionStartedEvent>(session.Events[0]);
    }

    private sealed class MutableWorkflowDefinitionProvider : IWorkflowDefinitionProvider
    {
        private WorkflowDefinition? _definition;

        public MutableWorkflowDefinitionProvider(WorkflowDefinition? definition)
        {
            _definition = definition;
        }

        public void Clear()
        {
            _definition = null;
        }

        public WorkflowDefinition? GetDefinition(string flowId)
        {
            return _definition is not null && string.Equals(_definition.FlowId?.Trim(), flowId, StringComparison.OrdinalIgnoreCase)
                ? _definition
                : null;
        }
    }
}
