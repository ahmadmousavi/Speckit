namespace ChangeFlow.Api.Models;

public enum ChangeRequestStatus
{
    Draft,
    PendingReview,
    ChangesRequested,
    Approved
}

public enum PermissionCapability
{
    Requester,
    Reviewer
}

public sealed class UserIdentity
{
    public Guid Id { get; set; }
}

public sealed class Permission
{
    public Guid UserId { get; set; }
    public PermissionCapability Capability { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class ChangeRequest
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? LastChangeRequestComment { get; set; }
    public Guid RequesterId { get; set; }
    public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.Draft;
    public Guid ConcurrencyToken { get; set; }
}

public sealed record ChangeRequestResponse(
    Guid Id,
    string Title,
    string Description,
    string Reason,
    string? LastChangeRequestComment,
    Guid RequesterId,
    ChangeRequestStatus Status,
    Guid ConcurrencyToken);

public sealed record CreateChangeRequestRequest(string Title, string Description, string Reason);

public sealed record EditChangeRequestRequest(string Title, string Description, string Reason, Guid ConcurrencyToken);

public sealed record MutationRequest(Guid ConcurrencyToken);

public sealed record RequestChangesRequest(Guid ConcurrencyToken, string Comment);
