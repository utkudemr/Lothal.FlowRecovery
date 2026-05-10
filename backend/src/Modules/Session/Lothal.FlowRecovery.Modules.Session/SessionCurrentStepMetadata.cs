namespace Lothal.FlowRecovery.Modules.Session;

public sealed record SessionCurrentStepMetadata
{
    public string ChangedBy { get; }
    public string ActorType { get; }
    public string? Reason { get; }

    private SessionCurrentStepMetadata(string changedBy, string actorType, string? reason)
    {
        ChangedBy = changedBy;
        ActorType = actorType;
        Reason = reason;
    }

    public static bool TryCreate(
        string? changedBy,
        string? actorType,
        string? reason,
        out SessionCurrentStepMetadata? metadata,
        out string? error)
    {
        metadata = null;
        error = null;

        var trimmedChangedBy = changedBy?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedChangedBy))
        {
            error = "ChangedBy is required.";
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

        metadata = new SessionCurrentStepMetadata(trimmedChangedBy, normalizedActorType, trimmedReason);
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
