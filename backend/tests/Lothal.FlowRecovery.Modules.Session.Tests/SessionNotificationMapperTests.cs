using Lothal.FlowRecovery.Modules.Session;

namespace Lothal.FlowRecovery.Modules.Session.Tests;

public sealed class SessionNotificationMapperTests
{
    [Fact]
    public void Map_ShouldConvertSessionCurrentStepSetEvent_ToStepChangedNotification()
    {
        var occurredAtUtc = new DateTime(2026, 4, 24, 20, 30, 0, DateTimeKind.Utc);
        var sessionId = Guid.NewGuid();
        var mapped = SessionNotificationMapper.Map(new SessionCurrentStepSetEvent(
            sessionId,
            "flow-1",
            "operator-b",
            "Operator",
            "manual correction",
            "cart",
            "payment",
            occurredAtUtc));

        var notification = Assert.IsType<StepChangedNotification>(mapped);

        Assert.Equal(sessionId, notification.SessionId);
        Assert.Equal("flow-1", notification.FlowId);
        Assert.Equal("payment", notification.CurrentStep);
        Assert.Equal("cart", notification.PreviousStep);
        Assert.Equal("operator-b", notification.ChangedBy);
        Assert.Equal("Operator", notification.ActorType);
        Assert.Equal("manual correction", notification.Reason);
        Assert.Equal(occurredAtUtc, notification.OccurredAtUtc);
    }

    [Fact]
    public void Map_ShouldPreserveNullPreviousStep_AndNullReason()
    {
        var occurredAtUtc = new DateTime(2026, 4, 24, 20, 30, 0, DateTimeKind.Utc);
        var sessionId = Guid.NewGuid();
        var mapped = SessionNotificationMapper.Map(new SessionCurrentStepSetEvent(
            sessionId,
            "flow-1",
            "operator-b",
            "Operator",
            null,
            null,
            "payment",
            occurredAtUtc));

        var notification = Assert.IsType<StepChangedNotification>(mapped);

        Assert.Null(notification.PreviousStep);
        Assert.Null(notification.Reason);
    }

    [Fact]
    public void Map_ShouldThrowNotSupportedException_ForNonStepEvents()
    {
        var exception = Assert.Throws<NotSupportedException>(() => SessionNotificationMapper.Map(new SessionStartedEvent(
            "flow-1",
            "operator-a",
            new DateTime(2026, 4, 24, 20, 30, 0, DateTimeKind.Utc))));

        Assert.Equal("Unsupported session event type: SessionStartedEvent.", exception.Message);
    }

    [Fact]
    public void Map_ShouldThrowArgumentNullException_ForNullInput()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => SessionNotificationMapper.Map(null!));

        Assert.Equal("@event", exception.ParamName);
    }
}
