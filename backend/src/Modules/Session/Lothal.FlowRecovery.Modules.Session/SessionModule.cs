using Lothal.FlowRecovery.Modules.Workflow;

namespace Lothal.FlowRecovery.Modules.Session;

public sealed class SessionModule
{
  private static readonly InMemorySessionStore SharedStore = new();
  private readonly StartSessionHandler _startSessionHandler;
  private readonly SetCurrentStepHandler _setCurrentStepHandler;
  private readonly EndSessionHandler _endSessionHandler;

  public SessionModule()
    : this(AllowAnySessionCurrentStepValidator.Instance)
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

  public IReadOnlyList<SessionSnapshot> ListActiveSessions()
  {
    return SharedStore.GetActiveSessions();
  }
}

public sealed record SessionSnapshot(
  Guid SessionId,
  string FlowId,
  string StartedBy,
  string Status,
  string? CurrentStep,
  DateTime StartedAtUtc,
  DateTime? EndedAtUtc,
  IReadOnlyList<SessionEvent> Events);

public interface IWorkflowDefinitionProvider
{
  WorkflowDefinition? GetDefinition(string flowId);
}

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

internal sealed class AllowAnySessionCurrentStepValidator : ISessionCurrentStepValidator
{
  public static readonly AllowAnySessionCurrentStepValidator Instance = new();

  private AllowAnySessionCurrentStepValidator()
  {
  }

  public SessionCurrentStepValidationResult Validate(string flowId, string? currentStep, string targetStep)
  {
    return SessionCurrentStepValidationResult.Allowed;
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
    var workflowDefinition = _workflowDefinitions.GetDefinition(flowId);
    if (workflowDefinition is null)
    {
      return SessionCurrentStepValidationResult.Rejected("Workflow definition not found.");
    }

    var normalizedTargetStep = targetStep?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(currentStep))
    {
      return ValidateInitialStep(workflowDefinition, flowId, normalizedTargetStep);
    }

    var transitionValidation = ValidateWorkflowTransition.Validate(workflowDefinition, flowId, currentStep, normalizedTargetStep);
    return transitionValidation.Outcome == ValidateWorkflowTransitionOutcome.Rejected
      ? SessionCurrentStepValidationResult.Rejected(transitionValidation.Error ?? "Workflow transition rejected.")
      : SessionCurrentStepValidationResult.Allowed;
  }

  private static SessionCurrentStepValidationResult ValidateInitialStep(WorkflowDefinition workflowDefinition, string flowId, string normalizedTargetStep)
  {
    if (workflowDefinition.Steps is null || workflowDefinition.AllowedTransitions is null)
    {
      return SessionCurrentStepValidationResult.Rejected("Workflow definition is incomplete.");
    }

    if (!string.Equals(workflowDefinition.FlowId, flowId, StringComparison.Ordinal))
    {
      return SessionCurrentStepValidationResult.Rejected("Workflow definition does not match FlowId.");
    }

    if (!ContainsStep(workflowDefinition.Steps, normalizedTargetStep))
    {
      return SessionCurrentStepValidationResult.Rejected("TargetStep is not defined.");
    }

    if (!TryGetWorkflowStartStep(workflowDefinition, out var workflowStartStep))
    {
      return SessionCurrentStepValidationResult.Rejected("Workflow definition is incomplete.");
    }

    if (!string.Equals(workflowStartStep, normalizedTargetStep, StringComparison.Ordinal))
    {
      return SessionCurrentStepValidationResult.Rejected("TargetStep must be workflow start step.");
    }

    return SessionCurrentStepValidationResult.Allowed;
  }

  private static bool TryGetWorkflowStartStep(WorkflowDefinition workflowDefinition, out string? workflowStartStep)
  {
    workflowStartStep = null;

    var definedSteps = new HashSet<string>(StringComparer.Ordinal);
    var orderedSteps = new List<string>();

    foreach (var candidate in workflowDefinition.Steps)
    {
      var normalized = candidate?.Trim() ?? string.Empty;
      if (string.IsNullOrWhiteSpace(normalized))
      {
        continue;
      }

      if (definedSteps.Add(normalized))
      {
        orderedSteps.Add(normalized);
      }
    }

    if (orderedSteps.Count == 0)
    {
      return false;
    }

    var incomingSteps = new HashSet<string>(StringComparer.Ordinal);
    foreach (var transition in workflowDefinition.AllowedTransitions)
    {
      var normalizedSourceStep = transition.Key?.Trim() ?? string.Empty;
      if (string.IsNullOrWhiteSpace(normalizedSourceStep) || !definedSteps.Contains(normalizedSourceStep))
      {
        return false;
      }

      var allowedTargets = transition.Value;
      if (allowedTargets is null)
      {
        return false;
      }

      foreach (var target in allowedTargets)
      {
        var normalizedTarget = target?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedTarget))
        {
          return false;
        }

        if (!definedSteps.Contains(normalizedTarget))
        {
          return false;
        }

        incomingSteps.Add(normalizedTarget);
      }
    }

    foreach (var candidate in orderedSteps)
    {
      if (incomingSteps.Contains(candidate))
      {
        continue;
      }

      if (workflowStartStep is not null)
      {
        workflowStartStep = null;
        return false;
      }

      workflowStartStep = candidate;
    }

    return workflowStartStep is not null;
  }

  private static bool ContainsStep(IReadOnlyCollection<string> steps, string step)
  {
    foreach (var candidate in steps)
    {
      if (string.Equals(candidate?.Trim(), step, StringComparison.Ordinal))
      {
        return true;
      }
    }

    return false;
  }
}
