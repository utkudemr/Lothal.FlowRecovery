using Lothal.FlowRecovery.Modules.Workflow;

namespace Lothal.FlowRecovery.Modules.Session;

public sealed class SessionModule
{
  private static readonly InMemorySessionStore SharedStore = new();
  private readonly StartSessionHandler _startSessionHandler;
  private readonly SetCurrentStepHandler _setCurrentStepHandler;
  private readonly EndSessionHandler _endSessionHandler;

  public SessionModule()
    : this(EmptyWorkflowDefinitionProvider.Instance)
  {
  }

  public SessionModule(IWorkflowDefinitionProvider workflowDefinitions)
    : this(new WorkflowSessionCurrentStepValidator(workflowDefinitions))
  {
  }

  private SessionModule(ISessionCurrentStepValidator currentStepValidator)
  {
    ArgumentNullException.ThrowIfNull(currentStepValidator);

    _startSessionHandler = new StartSessionHandler(SharedStore);
    _setCurrentStepHandler = new SetCurrentStepHandler(SharedStore, currentStepValidator);
    _endSessionHandler = new EndSessionHandler(SharedStore);
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
    if (!SharedStore.TryGetSnapshot(sessionId, out var snapshot))
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

    if (!SharedStore.TryGetActiveSnapshotByFlowId(flowId, out var snapshot))
    {
      return null;
    }

    return snapshot;
  }

  public IReadOnlyList<SessionSnapshot> ListActiveSessions()
  {
    return SharedStore.GetActiveSessions();
  }

  public IReadOnlyList<SessionSnapshot> ListStaleActiveSessions(DateTime staleBeforeUtc)
  {
    return SharedStore.GetStaleActiveSessions(staleBeforeUtc);
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
