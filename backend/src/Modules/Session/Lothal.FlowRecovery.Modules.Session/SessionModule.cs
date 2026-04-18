namespace Lothal.FlowRecovery.Modules.Session;

public sealed class SessionModule
{
  private static readonly InMemorySessionStore SharedStore = new();
  private readonly StartSessionHandler _startSessionHandler;

  public SessionModule()
  {
    _startSessionHandler = new StartSessionHandler(SharedStore);
  }

  public StartSessionResult StartSession(StartSessionCommand command)
  {
    return _startSessionHandler.Handle(command);
  }

  public SessionSnapshot? GetSession(Guid sessionId)
  {
    if (!SharedStore.TryGetSnapshot(sessionId, out var snapshot))
    {
      return null;
    }

    return snapshot;
  }
}

public sealed record SessionSnapshot(
  Guid SessionId,
  string FlowId,
  string StartedBy,
  string Status,
  DateTime StartedAtUtc,
  IReadOnlyList<SessionEvent> Events);
