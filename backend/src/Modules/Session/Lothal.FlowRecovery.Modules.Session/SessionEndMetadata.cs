namespace Lothal.FlowRecovery.Modules.Session;

public sealed record SessionEndMetadata
{
    public string EndedBy { get; }
    public string ActorType { get; }
    public string? Reason { get; }

    private SessionEndMetadata(string endedBy, string actorType, string? reason)
    {
        EndedBy = endedBy;
        ActorType = actorType;
        Reason = reason;
    }

    public static bool TryCreate(
        string? endedBy,
        string? actorType,
        string? reason,
        out SessionEndMetadata? metadata,
        out string? error)
    {
        metadata = null;
        error = null;

        var trimmedEndedBy = endedBy?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedEndedBy))
        {
            error = "EndedBy is required.";
            return false;
        }

        var trimmedActorType = actorType?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedActorType))
        {
            error = "ActorType is required.";
            return false;
        }

        var normalizedActorType = NormalizeActorType(trimmedActorType);
        if (normalizedActorType is null)
        {
            error = "ActorType is invalid.";
            return false;
        }

        var trimmedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedReason))
        {
            trimmedReason = null;
        }

        if (normalizedActorType == "Operator" && trimmedReason is null)
        {
            error = "Reason is required for operator end.";
            return false;
        }

        metadata = new SessionEndMetadata(trimmedEndedBy, normalizedActorType, trimmedReason);
        return true;
    }

    private static string? NormalizeActorType(string actorType)
    {
        if (string.Equals(actorType, "Operator", StringComparison.OrdinalIgnoreCase))
        {
            return "Operator";
        }

        if (string.Equals(actorType, "System", StringComparison.OrdinalIgnoreCase))
        {
            return "System";
        }

        return null;
    }
}
