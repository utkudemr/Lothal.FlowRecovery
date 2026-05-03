using Lothal.FlowRecovery.Modules.Session;

namespace Lothal.FlowRecovery.Modules.Realtime;

public sealed class RealtimeModule
{
    private readonly object _gate = new();
    private readonly List<Subscription> _subscriptions = new();

    public IDisposable Subscribe(Action<SessionNotification> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var subscription = new Subscription(this, handler);

        lock (_gate)
        {
            _subscriptions.Add(subscription);
        }

        return subscription;
    }

    public void Publish(SessionNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        Action<SessionNotification>[] handlers;

        lock (_gate)
        {
            handlers = _subscriptions
                .Select(subscription => subscription.Handler)
                .ToArray();
        }

        List<Exception>? failures = null;

        foreach (var handler in handlers)
        {
            try
            {
                handler(notification);
            }
            catch (Exception exception)
            {
                failures ??= new List<Exception>();
                failures.Add(exception);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException(failures);
        }
    }

    private void Unsubscribe(Subscription subscription)
    {
        lock (_gate)
        {
            _subscriptions.Remove(subscription);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private RealtimeModule? _module;

        public Subscription(
            RealtimeModule module,
            Action<SessionNotification> handler)
        {
            _module = module;
            Handler = handler;
        }

        public Action<SessionNotification> Handler { get; }

        public void Dispose()
        {
            var currentModule = Interlocked.Exchange(ref _module, null);

            currentModule?.Unsubscribe(this);
        }
    }
}
