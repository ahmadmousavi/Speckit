using ChangeFlow.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChangeFlow.Api.Data;

public sealed class PermissionStore(ChangeFlowDbContext db)
{
    public async Task RevokeAsync(Guid userId, PermissionCapability capability, CancellationToken cancellationToken = default)
    {
        var permission = await db.Permissions.SingleOrDefaultAsync(
            x => x.UserId == userId && x.Capability == capability, cancellationToken);

        if (permission is not null)
        {
            permission.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RestoreAsync(Guid userId, PermissionCapability capability, CancellationToken cancellationToken = default)
    {
        var permission = await db.Permissions.SingleOrDefaultAsync(
            x => x.UserId == userId && x.Capability == capability, cancellationToken);

        if (permission is not null)
        {
            permission.RevokedAt = null;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
