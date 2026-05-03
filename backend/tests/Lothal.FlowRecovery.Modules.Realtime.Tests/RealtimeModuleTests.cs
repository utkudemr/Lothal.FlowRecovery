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
    public void Publish_ShouldHandleConcurrentSubscribePublishAndDispose_WithoutDeadlock()
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

        var completed = Task.WhenAll(publishTask, mutationTask).Wait(TimeSpan.FromSeconds(5));

        Assert.True(completed);
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
