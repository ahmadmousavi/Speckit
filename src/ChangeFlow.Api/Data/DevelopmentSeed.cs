using ChangeFlow.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChangeFlow.Api.Data;

public static class DevelopmentSeed
{
    public static readonly Guid RequesterOnlyId = Guid.Parse("10000000-0000-4000-8000-000000000001");
    public static readonly Guid ReviewerOnlyId = Guid.Parse("20000000-0000-4000-8000-000000000002");
    public static readonly Guid DualRoleId = Guid.Parse("30000000-0000-4000-8000-000000000003");

    public static async Task SeedAsync(ChangeFlowDbContext db, CancellationToken cancellationToken = default)
    {
        var users = new[]
        {
            new UserIdentity { Id = RequesterOnlyId },
            new UserIdentity { Id = ReviewerOnlyId },
            new UserIdentity { Id = DualRoleId }
        };

        foreach (var user in users)
        {
            if (!await db.UserIdentities.AnyAsync(x => x.Id == user.Id, cancellationToken))
                db.UserIdentities.Add(user);
        }

        await EnsurePermissionAsync(db, RequesterOnlyId, PermissionCapability.Requester, cancellationToken);
        await EnsurePermissionAsync(db, ReviewerOnlyId, PermissionCapability.Reviewer, cancellationToken);
        await EnsurePermissionAsync(db, DualRoleId, PermissionCapability.Requester, cancellationToken);
        await EnsurePermissionAsync(db, DualRoleId, PermissionCapability.Reviewer, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsurePermissionAsync(ChangeFlowDbContext db, Guid userId,
        PermissionCapability capability, CancellationToken cancellationToken)
    {
        var existing = await db.Permissions.SingleOrDefaultAsync(
            x => x.UserId == userId && x.Capability == capability, cancellationToken);

        if (existing is null)
        {
            db.Permissions.Add(new Permission { UserId = userId, Capability = capability });
        }
        else if (existing.RevokedAt.HasValue)
        {
            existing.RevokedAt = null;
        }
    }
}
