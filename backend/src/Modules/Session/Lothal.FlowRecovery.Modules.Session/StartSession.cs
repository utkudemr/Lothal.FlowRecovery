using Lothal.FlowRecovery.Modules.Workflow;

namespace Lothal.FlowRecovery.Modules.Session;

public sealed record StartSessionCommand(string FlowId, string StartedBy);

public sealed record StartSessionResult(
    bool Success,
    Guid? SessionId,
    string FlowId,
    string Status,
    DateTime? StartedAtUtc,
    string? StartStep,
    string? Error,
    StartSessionOutcome? Outcome,
    SessionNotification? Notification);

public enum StartSessionOutcome
{
    Started,
    DuplicateActiveSession
}

internal sealed class StartSessionHandler
{
    private readonly InMemorySessionStore _store;
    private readonly IWorkflowDefinitionProvider _workflowDefinitions;

    public StartSessionHandler(InMemorySessionStore store, IWorkflowDefinitionProvider workflowDefinitions)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(workflowDefinitions);

        _store = store;
        _workflowDefinitions = workflowDefinitions;
    }

    public StartSessionResult Handle(StartSessionCommand command)
    {
        var flowId = command.FlowId?.Trim() ?? string.Empty;
        var startedBy = command.StartedBy?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(flowId))
        {
            return new StartSessionResult(false, null, string.Empty, "Rejected", null, null, "FlowId is required.", null, null);
        }

        if (string.IsNullOrWhiteSpace(startedBy))
        {
            return new StartSessionResult(false, null, flowId, "Rejected", null, null, "StartedBy is required.", null, null);
        }

        var startedAtUtc = DateTime.UtcNow;
        var sessionId = Guid.NewGuid();

        if (!_store.TrySaveIfNoActiveSession(sessionId, flowId, startedBy, startedAtUtc, startedBy, out var session, out var activeSession, out var startedEvent))
        {
            return new StartSessionResult(
                false,
                activeSession!.SessionId,
                activeSession.FlowId,
                activeSession.Status,
                activeSession.StartedAtUtc,
                ResolveStartStep(activeSession.FlowId),
                "Active session already exists.",
                StartSessionOutcome.DuplicateActiveSession,
                null);
        }

        var notification = SessionNotificationMapper.Map(startedEvent!);
        if (notification is null)
        {
            throw new InvalidOperationException("Invariant violation: started outcome must produce a start-session notification.");
        }

        return new StartSessionResult(true, session!.SessionId, session.FlowId, session.Status, session.StartedAtUtc, ResolveStartStep(session.FlowId), null, StartSessionOutcome.Started, notification);
    }

    private string? ResolveStartStep(string flowId)
    {
        var definition = _workflowDefinitions.GetDefinition(flowId);
        if (definition is null)
        {
            return null;
        }

        if (!string.Equals(definition.FlowId?.Trim(), flowId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var query = ValidateWorkflowInitialStep.QueryStartStep(definition);
        return query.Success ? query.StartStep : null;
    }
}
