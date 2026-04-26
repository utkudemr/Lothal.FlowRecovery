using Lothal.FlowRecovery.Modules.Session;

namespace Lothal.FlowRecovery.Modules.Session.Tests;

internal sealed record UnsupportedSessionEvent(DateTime OccurredAtUtc) : SessionEvent(OccurredAtUtc);

public sealed class SessionNotificationMapperTests
{
    [Fact]
    public void Map_ShouldConvertSessionStartedEvent_ToSessionStartedNotification()
    {
        var occurredAtUtc = new DateTime(2026, 4, 24, 20, 25, 0, DateTimeKind.Utc);
        var sessionId = Guid.NewGuid();
        var mapped = SessionNotificationMapper.Map(new SessionStartedEvent(
            sessionId,
            "flow-1",
            "operator-a",
            occurredAtUtc));

        var notification = Assert.IsType<SessionStartedNotification>(mapped);

        Assert.Equal(sessionId, notification.SessionId);
        Assert.Equal("flow-1", notification.FlowId);
        Assert.Equal("operator-a", notification.StartedBy);
        Assert.Equal(occurredAtUtc, notification.OccurredAtUtc);
    }

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
    public void Map_ShouldConvertSessionEndedEvent_ToSessionEndedNotification()
    {
        var occurredAtUtc = new DateTime(2026, 4, 24, 20, 35, 0, DateTimeKind.Utc);
        var sessionId = Guid.NewGuid();
        var mapped = SessionNotificationMapper.Map(new SessionEndedEvent(
            sessionId,
            "flow-1",
            "operator-b",
            "System",
            null,
            "Active",
            "Ended",
            occurredAtUtc));

        var notification = Assert.IsType<SessionEndedNotification>(mapped);

        Assert.Equal(sessionId, notification.SessionId);
        Assert.Equal("flow-1", notification.FlowId);
        Assert.Equal("operator-b", notification.EndedBy);
        Assert.Equal("System", notification.ActorType);
        Assert.Null(notification.Reason);
        Assert.Equal("Active", notification.PreviousStatus);
        Assert.Equal("Ended", notification.NewStatus);
        Assert.Equal(occurredAtUtc, notification.OccurredAtUtc);
    }

    [Fact]
    public void Map_ShouldReturnNull_ForSessionEndAlreadyEndedAuditEvent()
    {
        var mapped = SessionNotificationMapper.Map(new SessionEndAlreadyEndedAuditEvent(
            Guid.NewGuid(),
            "flow-1",
            "operator-b",
            "Operator",
            "duplicate",
            "Ended",
            DateTime.UtcNow,
            new DateTime(2026, 4, 24, 20, 40, 0, DateTimeKind.Utc)));

        Assert.Null(mapped);
    }

    [Fact]
    public void Map_ShouldReturnNull_ForSessionCurrentStepUnchangedEvent()
    {
        var mapped = SessionNotificationMapper.Map(new SessionCurrentStepUnchangedEvent(
            Guid.NewGuid(),
            "flow-1",
            "cart",
            "payment",
            "operator-b",
            "Operator",
            null,
            "Unchanged",
            new DateTime(2026, 4, 24, 20, 41, 0, DateTimeKind.Utc)));

        Assert.Null(mapped);
    }

    [Fact]
    public void Map_ShouldReturnNull_ForSessionCurrentStepRejectedNotActiveEvent()
    {
        var mapped = SessionNotificationMapper.Map(new SessionCurrentStepRejectedNotActiveEvent(
            Guid.NewGuid(),
            "flow-1",
            "cart",
            "payment",
            "operator-b",
            "Operator",
            "session already ended",
            "Ended",
            new DateTime(2026, 4, 24, 20, 42, 0, DateTimeKind.Utc)));

        Assert.Null(mapped);
    }

    [Fact]
    public void Map_ShouldThrowArgumentNullException_ForNullInput()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => SessionNotificationMapper.Map(null!));

        Assert.Equal("@event", exception.ParamName);
    }

    [Fact]
    public void Map_ShouldThrowNotSupportedException_ForUnsupportedSessionEvent()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            SessionNotificationMapper.Map(new UnsupportedSessionEvent(
                new DateTime(2026, 4, 24, 20, 45, 0, DateTimeKind.Utc))));

        Assert.Contains(nameof(UnsupportedSessionEvent), exception.Message);
    }
}
