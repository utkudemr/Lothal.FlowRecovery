using Lothal.FlowRecovery.Modules.Realtime;
using Lothal.FlowRecovery.Modules.Session;
using Lothal.FlowRecovery.Modules.Workflow;
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
    public void SubscribeToSession_ShouldDeliverMatchingNotification()
    {
        var module = new RealtimeModule();
        var sessionId = Guid.NewGuid();
        SessionNotification? delivered = null;

        module.SubscribeToSession(sessionId, notification => delivered = notification);

        var notification = CreateStepChangedNotification(sessionId);
        module.Publish(notification);

        Assert.Same(notification, delivered);
    }

    [Fact]
    public void SubscribeToFlow_ShouldDeliverMatchingNotification()
    {
        var module = new RealtimeModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var subscriptionFlowId = $"  {flowId.ToUpperInvariant()}  ";
        var deliveredNotifications = new List<SessionNotification>();

        module.SubscribeToFlow(subscriptionFlowId, notification => deliveredNotifications.Add(notification));

        var started = new SessionStartedNotification(Guid.NewGuid(), flowId, "operator-a", new DateTime(2026, 5, 2, 18, 45, 0, DateTimeKind.Utc));
        var changed = new StepChangedNotification(Guid.NewGuid(), flowId, "step-2", "step-1", "operator-a", "Operator", null, new DateTime(2026, 5, 2, 18, 46, 0, DateTimeKind.Utc));
        var ended = new SessionEndedNotification(Guid.NewGuid(), flowId, "operator-a", "Operator", null, "Active", "Ended", new DateTime(2026, 5, 2, 18, 47, 0, DateTimeKind.Utc));

        module.Publish(started);
        module.Publish(changed);
        module.Publish(ended);

        Assert.Equal(3, deliveredNotifications.Count);
        Assert.Same(started, deliveredNotifications[0]);
        Assert.Same(changed, deliveredNotifications[1]);
        Assert.Same(ended, deliveredNotifications[2]);
    }

    [Fact]
    public void SubscribeToFlow_ShouldSuppressNonMatchingNotification()
    {
        var module = new RealtimeModule();
        var subscribedFlowId = $"flow-{Guid.NewGuid():N}";
        var otherFlowId = $"flow-{Guid.NewGuid():N}";
        var deliveredCount = 0;

        module.SubscribeToFlow(subscribedFlowId, _ => deliveredCount++);

        module.Publish(CreateNotification(Guid.NewGuid(), otherFlowId));
        module.Publish(CreateStepChangedNotification(Guid.NewGuid(), otherFlowId));
        module.Publish(CreateEndedNotification(Guid.NewGuid(), otherFlowId));

        Assert.Equal(0, deliveredCount);
    }

    [Fact]
    public void SubscribeToSession_ShouldDeliverSubscribedSessionLifecycleNotifications_AndIgnoreOtherSession()
    {
        var subscribedFlowId = $"flow-{Guid.NewGuid():N}";
        var otherFlowId = $"flow-{Guid.NewGuid():N}";
        var realtime = new RealtimeModule();
        var session = new SessionModule(new TestWorkflowDefinitionProvider(
            new WorkflowDefinition(subscribedFlowId, new[] { "cart", "payment", "review", "confirm" }, new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
            {
                ["cart"] = new[] { "payment" },
                ["payment"] = new[] { "review" },
                ["review"] = new[] { "confirm" },
                ["confirm"] = Array.Empty<string>(),
            }),
            new WorkflowDefinition(otherFlowId, new[] { "cart", "payment", "review", "confirm" }, new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
            {
                ["cart"] = new[] { "payment" },
                ["payment"] = new[] { "review" },
                ["review"] = new[] { "confirm" },
                ["confirm"] = Array.Empty<string>(),
            })));
        var deliveredNotifications = new List<SessionNotification>();

        var subscribedStart = session.StartSession(new StartSessionCommand(subscribedFlowId, "operator-a"));
        var otherStart = session.StartSession(new StartSessionCommand(otherFlowId, "operator-b"));
        var subscribedStep = session.SetCurrentStep(new SetCurrentStepCommand(subscribedStart.SessionId!.Value, "cart", "operator-c", "System", null));
        var otherStep = session.SetCurrentStep(new SetCurrentStepCommand(otherStart.SessionId!.Value, "cart", "operator-d", "System", null));
        var subscribedEnd = session.EndSession(new EndSessionCommand(subscribedStart.SessionId.Value, "operator-e", "Operator", "done"));
        var otherEnd = session.EndSession(new EndSessionCommand(otherStart.SessionId.Value, "operator-f", "Operator", "done"));

        Assert.True(subscribedStart.Success);
        Assert.True(otherStart.Success);
        Assert.True(subscribedStep.Success);
        Assert.True(otherStep.Success);
        Assert.True(subscribedEnd.Success);
        Assert.True(otherEnd.Success);

        realtime.SubscribeToSession(subscribedStart.SessionId!.Value, notification => deliveredNotifications.Add(notification));

        Assert.True(realtime.TryPublish(otherStart.Notification));
        Assert.True(realtime.TryPublish(subscribedStart.Notification));
        Assert.True(realtime.TryPublish(otherStep.Notification));
        Assert.True(realtime.TryPublish(subscribedStep.Notification));
        Assert.True(realtime.TryPublish(otherEnd.Notification));
        Assert.True(realtime.TryPublish(subscribedEnd.Notification));

        Assert.Collection(deliveredNotifications,
            notification =>
            {
                var started = Assert.IsType<SessionStartedNotification>(notification);
                Assert.Equal(subscribedStart.SessionId.Value, started.SessionId);
                Assert.Equal(subscribedFlowId, started.FlowId);
                Assert.Equal("operator-a", started.StartedBy);
            },
            notification =>
            {
                var stepChanged = Assert.IsType<StepChangedNotification>(notification);
                Assert.Equal(subscribedStart.SessionId.Value, stepChanged.SessionId);
                Assert.Equal(subscribedFlowId, stepChanged.FlowId);
                Assert.Equal("cart", stepChanged.CurrentStep);
            },
            notification =>
            {
                var ended = Assert.IsType<SessionEndedNotification>(notification);
                Assert.Equal(subscribedStart.SessionId.Value, ended.SessionId);
                Assert.Equal(subscribedFlowId, ended.FlowId);
                Assert.Equal("Ended", ended.NewStatus);
            });

        Assert.DoesNotContain(deliveredNotifications, notification =>
            notification is SessionStartedNotification started && started.SessionId == otherStart.SessionId
            || notification is StepChangedNotification stepChanged && stepChanged.SessionId == otherStart.SessionId
            || notification is SessionEndedNotification ended && ended.SessionId == otherStart.SessionId);
    }

    [Fact]
    public void SubscribeToFlow_ShouldDeliverSubscribedSessionLifecycleNotifications_AndIgnoreOtherFlow()
    {
        var subscribedFlowId = $"flow-{Guid.NewGuid():N}";
        var otherFlowId = $"flow-{Guid.NewGuid():N}";
        var realtime = new RealtimeModule();
        var session = new SessionModule(new TestWorkflowDefinitionProvider(
            new WorkflowDefinition(subscribedFlowId, new[] { "cart", "payment", "review", "confirm" }, new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
            {
                ["cart"] = new[] { "payment" },
                ["payment"] = new[] { "review" },
                ["review"] = new[] { "confirm" },
                ["confirm"] = Array.Empty<string>(),
            }),
            new WorkflowDefinition(otherFlowId, new[] { "cart", "payment", "review", "confirm" }, new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
            {
                ["cart"] = new[] { "payment" },
                ["payment"] = new[] { "review" },
                ["review"] = new[] { "confirm" },
                ["confirm"] = Array.Empty<string>(),
            })));
        var deliveredNotifications = new List<SessionNotification>();

        realtime.SubscribeToFlow(subscribedFlowId, notification => deliveredNotifications.Add(notification));

        var otherStart = session.StartSession(new StartSessionCommand(otherFlowId, "operator-b"));
        var subscribedStart = session.StartSession(new StartSessionCommand(subscribedFlowId, "operator-a"));
        var otherStep = session.SetCurrentStep(new SetCurrentStepCommand(otherStart.SessionId!.Value, "cart", "operator-c", "System", null));
        var otherEnd = session.EndSession(new EndSessionCommand(otherStart.SessionId.Value, "operator-d", "System", null));
        var subscribedStep = session.SetCurrentStep(new SetCurrentStepCommand(subscribedStart.SessionId!.Value, "cart", "operator-c", "System", null));
        var subscribedEnd = session.EndSession(new EndSessionCommand(subscribedStart.SessionId.Value, "operator-d", "System", null));

        Assert.True(otherStart.Success);
        Assert.True(subscribedStart.Success);
        Assert.True(otherStep.Success);
        Assert.True(otherEnd.Success);
        Assert.True(subscribedStep.Success);
        Assert.True(subscribedEnd.Success);
        Assert.True(realtime.TryPublish(otherStart.Notification));
        Assert.True(realtime.TryPublish(subscribedStart.Notification));
        Assert.True(realtime.TryPublish(otherStep.Notification));
        Assert.True(realtime.TryPublish(otherEnd.Notification));
        Assert.True(realtime.TryPublish(subscribedStep.Notification));
        Assert.True(realtime.TryPublish(subscribedEnd.Notification));

        Assert.Collection(deliveredNotifications,
            notification =>
            {
                var started = Assert.IsType<SessionStartedNotification>(notification);
                Assert.Equal(subscribedFlowId, started.FlowId);
                Assert.Equal("operator-a", started.StartedBy);
            },
            notification =>
            {
                var stepChanged = Assert.IsType<StepChangedNotification>(notification);
                Assert.Equal(subscribedFlowId, stepChanged.FlowId);
                Assert.Equal("cart", stepChanged.CurrentStep);
            },
            notification =>
            {
                var ended = Assert.IsType<SessionEndedNotification>(notification);
                Assert.Equal(subscribedFlowId, ended.FlowId);
                Assert.Equal("Ended", ended.NewStatus);
            });

        Assert.DoesNotContain(deliveredNotifications, notification =>
            notification is SessionStartedNotification started && started.FlowId == otherFlowId
            || notification is StepChangedNotification stepChanged && stepChanged.FlowId == otherFlowId
            || notification is SessionEndedNotification ended && ended.FlowId == otherFlowId);
    }

    [Fact]
    public void SubscribeToFlow_ShouldStopDelivery_AfterDisposal()
    {
        var module = new RealtimeModule();
        var flowId = $"flow-{Guid.NewGuid():N}";
        var deliveredCount = 0;
        var subscription = module.SubscribeToFlow(flowId, _ => deliveredCount++);

        subscription.Dispose();
        module.Publish(CreateNotification(Guid.NewGuid(), flowId));

        Assert.Equal(0, deliveredCount);
    }

    [Fact]
    public void SubscribeToSession_ShouldSuppressNonMatchingNotification()
    {
        var module = new RealtimeModule();
        var subscribedSessionId = Guid.NewGuid();
        var otherSessionId = Guid.NewGuid();
        var deliveredCount = 0;

        module.SubscribeToSession(subscribedSessionId, _ => deliveredCount++);

        module.Publish(CreateEndedNotification(otherSessionId));

        Assert.Equal(0, deliveredCount);
    }

    [Fact]
    public void SubscribeToSession_ShouldStopDelivery_AfterDisposal()
    {
        var module = new RealtimeModule();
        var sessionId = Guid.NewGuid();
        var deliveredCount = 0;
        var subscription = module.SubscribeToSession(sessionId, _ => deliveredCount++);

        subscription.Dispose();
        module.Publish(CreateNotification(sessionId));

        Assert.Equal(0, deliveredCount);
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
    public void SubscribeToSession_ShouldThrowArgumentException_ForEmptySessionId()
    {
        var module = new RealtimeModule();

        var exception = Assert.Throws<ArgumentException>(() => module.SubscribeToSession(Guid.Empty, _ => { }));

        Assert.Equal("sessionId", exception.ParamName);
    }

    [Fact]
    public void SubscribeToFlow_ShouldThrowArgumentException_ForBlankFlowId()
    {
        var module = new RealtimeModule();

        var exception = Assert.Throws<ArgumentException>(() => module.SubscribeToFlow("   ", _ => { }));

        Assert.Equal("flowId", exception.ParamName);
    }

    [Fact]
    public void SubscribeToSession_ShouldThrowArgumentNullException_ForNullHandler()
    {
        var module = new RealtimeModule();

        var exception = Assert.Throws<ArgumentNullException>(() => module.SubscribeToSession(Guid.NewGuid(), null!));

        Assert.Equal("handler", exception.ParamName);
    }

    [Fact]
    public void SubscribeToFlow_ShouldThrowArgumentNullException_ForNullHandler()
    {
        var module = new RealtimeModule();

        var exception = Assert.Throws<ArgumentNullException>(() => module.SubscribeToFlow($"flow-{Guid.NewGuid():N}", null!));

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
        CreateNotification(Guid.NewGuid());

    private static SessionNotification CreateNotification(Guid sessionId) =>
        CreateNotification(sessionId, "flow-1");

    private static SessionNotification CreateNotification(Guid sessionId, string flowId) =>
        new SessionStartedNotification(
            sessionId,
            flowId,
            "operator-a",
            new DateTime(2026, 5, 2, 18, 45, 0, DateTimeKind.Utc));

    private static SessionNotification CreateStepChangedNotification(Guid sessionId) =>
        CreateStepChangedNotification(sessionId, "flow-1");

    private static SessionNotification CreateStepChangedNotification(Guid sessionId, string flowId) =>
        new StepChangedNotification(
            sessionId,
            flowId,
            "step-2",
            "step-1",
            "operator-a",
            "Operator",
            null,
            new DateTime(2026, 5, 2, 18, 46, 0, DateTimeKind.Utc));

    private static SessionNotification CreateEndedNotification(Guid sessionId) =>
        CreateEndedNotification(sessionId, "flow-1");

    private static SessionNotification CreateEndedNotification(Guid sessionId, string flowId) =>
        new SessionEndedNotification(
            sessionId,
            flowId,
            "operator-a",
            "Operator",
            null,
            "Active",
            "Ended",
            new DateTime(2026, 5, 2, 18, 47, 0, DateTimeKind.Utc));

    private sealed class TestWorkflowDefinitionProvider : IWorkflowDefinitionProvider
    {
        private readonly Dictionary<string, WorkflowDefinition> _definitions;

        public TestWorkflowDefinitionProvider(params WorkflowDefinition[] definitions)
        {
            _definitions = definitions.ToDictionary(definition => definition.FlowId, StringComparer.Ordinal);
        }

        public WorkflowDefinition? GetDefinition(string flowId)
        {
            return _definitions.TryGetValue(flowId, out var definition)
                ? definition
                : null;
        }
    }
}
