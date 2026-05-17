using Lothal.FlowRecovery.Modules.Workflow;

namespace Lothal.FlowRecovery.Modules.Session;

public sealed class SessionModule
{
  private static readonly InMemorySessionStore SharedStore = new();
  private readonly InMemorySessionStore _store;
  private readonly StartSessionHandler _startSessionHandler;
  private readonly SetCurrentStepHandler _setCurrentStepHandler;
  private readonly EndSessionHandler _endSessionHandler;

  public SessionModule()
    : this(SharedStore, EmptyWorkflowDefinitionProvider.Instance)
  {
  }

  public SessionModule(IWorkflowDefinitionProvider workflowDefinitions)
    : this(SharedStore, workflowDefinitions)
  {
  }

  internal SessionModule(InMemorySessionStore store)
    : this(store, EmptyWorkflowDefinitionProvider.Instance)
  {
  }

  private SessionModule(InMemorySessionStore store, IWorkflowDefinitionProvider workflowDefinitions)
    : this(store, workflowDefinitions, new WorkflowSessionCurrentStepValidator(workflowDefinitions))
  {
  }

  private SessionModule(
    InMemorySessionStore store,
    IWorkflowDefinitionProvider workflowDefinitions,
    ISessionCurrentStepValidator currentStepValidator)
  {
    ArgumentNullException.ThrowIfNull(store);
    ArgumentNullException.ThrowIfNull(workflowDefinitions);
    ArgumentNullException.ThrowIfNull(currentStepValidator);

    _store = store;
    _startSessionHandler = new StartSessionHandler(_store, workflowDefinitions);
    _setCurrentStepHandler = new SetCurrentStepHandler(_store, currentStepValidator);
    _endSessionHandler = new EndSessionHandler(_store);
  }

  public StartSessionResult StartSession(StartSessionCommand command)
  {
    return _startSessionHandler.Handle(command);
  }

  public SetCurrentStepResult SetCurrentStep(SetCurrentStepCommand command)
  {
    return _setCurrentStepHandler.Handle(command);
  }

  public EndSessionResult EndSession(EndSessionCommand command)
  {
    return _endSessionHandler.Handle(command);
  }

  public SessionSnapshot? GetSession(Guid sessionId)
  {
    if (!_store.TryGetSnapshot(sessionId, out var snapshot))
    {
      return null;
    }

    return snapshot;
  }

  public SessionSnapshot? GetActiveSessionByFlowId(string flowId)
  {
    if (string.IsNullOrWhiteSpace(flowId))
    {
      return null;
    }

    if (!_store.TryGetActiveSnapshotByFlowId(flowId, out var snapshot))
    {
      return null;
    }

    return snapshot;
  }

  public IReadOnlyList<SessionSnapshot> ListActiveSessions()
  {
    return _store.GetActiveSessions();
  }

  public IReadOnlyList<SessionSnapshot> ListStaleActiveSessions(DateTime staleBeforeUtc)
  {
    return _store.GetStaleActiveSessions(staleBeforeUtc);
  }
}

public sealed record SessionSnapshot(
  Guid SessionId,
  string FlowId,
  string StartedBy,
  string Status,
  string? CurrentStep,
  DateTime StartedAtUtc,
  DateTime LastEventAtUtc,
  string LastEventType,
  DateTime? EndedAtUtc,
  IReadOnlyList<SessionEvent> Events);

internal interface ISessionCurrentStepValidator
{
  SessionCurrentStepValidationResult Validate(string flowId, string? currentStep, string targetStep);
}

internal sealed record SessionCurrentStepValidationResult(bool Success, string? Error)
{
  public static readonly SessionCurrentStepValidationResult Allowed = new(true, null);

  public static SessionCurrentStepValidationResult Rejected(string error)
  {
    return new SessionCurrentStepValidationResult(false, error);
  }
}

internal sealed class EmptyWorkflowDefinitionProvider : IWorkflowDefinitionProvider
{
  public static readonly EmptyWorkflowDefinitionProvider Instance = new();

  private EmptyWorkflowDefinitionProvider()
  {
  }

  public WorkflowDefinition? GetDefinition(string flowId)
  {
    return null;
  }
}

internal sealed class WorkflowSessionCurrentStepValidator : ISessionCurrentStepValidator
{
  private readonly IWorkflowDefinitionProvider _workflowDefinitions;

  public WorkflowSessionCurrentStepValidator(IWorkflowDefinitionProvider workflowDefinitions)
  {
    ArgumentNullException.ThrowIfNull(workflowDefinitions);

    _workflowDefinitions = workflowDefinitions;
  }

  public SessionCurrentStepValidationResult Validate(string flowId, string? currentStep, string targetStep)
  {
    var validation = ValidateWorkflowCurrentStep.Validate(_workflowDefinitions, flowId, currentStep, targetStep);
    return validation.Success
      ? SessionCurrentStepValidationResult.Allowed
      : SessionCurrentStepValidationResult.Rejected(validation.Error ?? "Workflow transition rejected.");
  }
}
