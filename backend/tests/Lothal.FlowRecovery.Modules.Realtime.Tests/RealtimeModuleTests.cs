using Lothal.FlowRecovery.Modules.Realtime;
using Lothal.FlowRecovery.Modules.Session;
using System.Collections.Concurrent;

namespace Lothal.FlowRecovery.Modules.Realtime.Tests;

public sealed class RealtimeModuleTests
{
    [Fact]
    public void Publish_ShouldDeliverNotification_ToSubscriber()
    {
        var module = new RealtimeModule();
        var notification = CreateNotification();
        SessionNotification? delivered = null;

        module.Subscribe(received => delivered = received);

        module.Publish(notification);

        Assert.Same(notification, delivered);
    }

    [Fact]
    public void TryPublish_ShouldDeliverNotification_ToSubscriber()
    {
        var module = new RealtimeModule();
        var notification = CreateNotification();
        SessionNotification? delivered = null;

        module.Subscribe(received => delivered = received);

        var published = module.TryPublish(notification);

        Assert.True(published);
        Assert.Same(notification, delivered);
    }

    [Fact]
    public void TryPublish_ShouldReturnFalse_AndNotDeliver_WhenNotificationIsNull()
    {
        var module = new RealtimeModule();
        var deliveredCount = 0;

        module.Subscribe(_ => deliveredCount++);

        var published = module.TryPublish(null);

        Assert.False(published);
        Assert.Equal(0, deliveredCount);
    }

    [Fact]
    public void Publish_ShouldNotDeliverNotification_AfterUnsubscribe()
    {
        var module = new RealtimeModule();
        var deliveredCount = 0;
        var subscription = module.Subscribe(_ => deliveredCount++);

        subscription.Dispose();
        module.Publish(CreateNotification());

        Assert.Equal(0, deliveredCount);
    }

    [Fact]
    public void Publish_ShouldDeliverSameNotification_ToMultipleSubscribers()
    {
        var module = new RealtimeModule();
        var notification = CreateNotification();
        SessionNotification? first = null;
        SessionNotification? second = null;

        module.Subscribe(received => first = received);
        module.Subscribe(received => second = received);

        module.Publish(notification);

        Assert.Same(notification, first);
        Assert.Same(notification, second);
    }

    [Fact]
    public void TryPublish_ShouldDeliverStartSessionNotification_FromSessionModule()
    {
        var realtime = new RealtimeModule();
        var session = new SessionModule();
        SessionNotification? delivered = null;

        realtime.Subscribe(notification => delivered = notification);

        var result = session.StartSession(new StartSessionCommand($"flow-{Guid.NewGuid():N}", "operator-a"));

        Assert.True(realtime.TryPublish(result.Notification));
        Assert.IsType<SessionStartedNotification>(delivered);
        Assert.Same(result.Notification, delivered);
    }

    [Fact]
    public void TryPublish_ShouldDeliverEndSessionNotification_FromSessionModule()
    {
        var realtime = new RealtimeModule();
        var session = new SessionModule();
        SessionNotification? delivered = null;

        realtime.Subscribe(notification => delivered = notification);

        var started = session.StartSession(new StartSessionCommand($"flow-{Guid.NewGuid():N}", "operator-a"));
        var ended = session.EndSession(new EndSessionCommand(started.SessionId!.Value, "operator-b", "Operator", "completed"));

        Assert.True(realtime.TryPublish(ended.Notification));
        Assert.IsType<SessionEndedNotification>(delivered);
        Assert.Same(ended.Notification, delivered);
    }

    [Fact]
    public void Publish_ShouldContinueDelivery_AndThrowAggregateException_WhenSubscriberThrows()
    {
        var module = new RealtimeModule();
        var notification = CreateNotification();
        var subscriberException = new InvalidOperationException("subscriber failed");
        SessionNotification? delivered = null;

        module.Subscribe(_ => throw subscriberException);
        module.Subscribe(received => delivered = received);

        var exception = Assert.Throws<AggregateException>(() => module.Publish(notification));

        Assert.Same(notification, delivered);
        var failure = Assert.Single(exception.InnerExceptions);
        Assert.Same(subscriberException, failure);
        Assert.Contains("subscriber failed", failure.Message);
    }

    [Fact]
    public async Task Publish_ShouldHandleConcurrentSubscribePublishAndDispose_WithoutDeadlock()
    {
        var module = new RealtimeModule();
        var exceptions = new ConcurrentQueue<Exception>();
        var notificationsSeen = 0;

        using var stableSubscription = module.Subscribe(_ => Interlocked.Increment(ref notificationsSeen));

        var publishTask = Task.Run(() =>
        {
            try
            {
                for (var i = 0; i < 200; i++)
                {
                    module.Publish(CreateNotification());
                }
            }
            catch (Exception exception)
            {
                exceptions.Enqueue(exception);
            }
        });

        var mutationTask = Task.Run(() =>
        {
            try
            {
                for (var i = 0; i < 200; i++)
                {
                    var subscription = module.Subscribe(_ => { });
                    subscription.Dispose();
                }
            }
            catch (Exception exception)
            {
                exceptions.Enqueue(exception);
            }
        });

        await Task.WhenAll(publishTask, mutationTask).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(exceptions.IsEmpty);
        Assert.True(notificationsSeen > 0);
    }

    [Fact]
    public void Publish_ShouldAllowHandlerToUnsubscribeItself_DuringDelivery()
    {
        var module = new RealtimeModule();
        var firstDelivered = 0;
        var secondDelivered = 0;
        IDisposable? selfSubscription = null;

        selfSubscription = module.Subscribe(_ =>
        {
            Interlocked.Increment(ref firstDelivered);
            selfSubscription!.Dispose();
        });

        module.Subscribe(_ => Interlocked.Increment(ref secondDelivered));

        module.Publish(CreateNotification());
        module.Publish(CreateNotification());

        Assert.Equal(1, Volatile.Read(ref firstDelivered));
        Assert.Equal(2, Volatile.Read(ref secondDelivered));
    }

    [Fact]
    public void Publish_ShouldDeliverCurrentNotification_WhenHandlerUnsubscribesAnotherSubscriber()
    {
        var module = new RealtimeModule();
        var firstDelivered = 0;
        var secondDelivered = 0;
        IDisposable? secondSubscription = null;

        module.Subscribe(_ =>
        {
            Interlocked.Increment(ref firstDelivered);
            secondSubscription!.Dispose();
        });

        secondSubscription = module.Subscribe(_ => Interlocked.Increment(ref secondDelivered));

        module.Publish(CreateNotification());
        module.Publish(CreateNotification());

        Assert.Equal(2, Volatile.Read(ref firstDelivered));
        Assert.Equal(1, Volatile.Read(ref secondDelivered));
    }

    [Fact]
    public async Task Publish_ShouldHandleConcurrentPublishesWithSubscribeDisposeChurn_WithoutDeadlockOrInternalExceptions()
    {
        var module = new RealtimeModule();
        var exceptions = new ConcurrentQueue<Exception>();
        var notificationsSeen = 0;
        using var startGate = new ManualResetEventSlim();
        using var stableSubscription = module.Subscribe(_ => Interlocked.Increment(ref notificationsSeen));

        const int publisherCount = 4;
        const int publishIterations = 200;
        const int mutationIterations = 500;

        var publishTasks = Enumerable.Range(0, publisherCount)
            .Select(_ => Task.Run(() =>
            {
                try
                {
                    startGate.Wait();

                    for (var i = 0; i < publishIterations; i++)
                    {
                        module.Publish(CreateNotification());
                    }
                }
                catch (Exception exception)
                {
                    exceptions.Enqueue(exception);
                }
            }))
            .ToArray();

        var mutationTask = Task.Run(() =>
        {
            try
            {
                startGate.Wait();

                for (var i = 0; i < mutationIterations; i++)
                {
                    var subscription = module.Subscribe(_ => { });
                    subscription.Dispose();
                }
            }
            catch (Exception exception)
            {
                exceptions.Enqueue(exception);
            }
        });

        startGate.Set();
        await Task.WhenAll(publishTasks.Append(mutationTask)).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(exceptions.IsEmpty);
        Assert.Equal(publisherCount * publishIterations, Volatile.Read(ref notificationsSeen));
    }

    [Fact]
    public void Subscribe_ShouldThrowArgumentNullException_ForNullHandler()
    {
        var module = new RealtimeModule();

        var exception = Assert.Throws<ArgumentNullException>(() => module.Subscribe(null!));

        Assert.Equal("handler", exception.ParamName);
    }

    [Fact]
    public void Publish_ShouldThrowArgumentNullException_ForNullNotification()
    {
        var module = new RealtimeModule();

        var exception = Assert.Throws<ArgumentNullException>(() => module.Publish(null!));

        Assert.Equal("notification", exception.ParamName);
    }

    private static SessionNotification CreateNotification() =>
        new SessionStartedNotification(
            Guid.NewGuid(),
            "flow-1",
            "operator-a",
            new DateTime(2026, 5, 2, 18, 45, 0, DateTimeKind.Utc));
}
