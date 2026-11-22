using ChangeFlow.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChangeFlow.Api.Data;

public sealed class PermissionStore(
    IServiceScopeFactory scopeFactory,
    SqliteImmediateTransaction transactions)
{
    public async Task RevokeAsync(Guid userId, PermissionCapability capability, CancellationToken cancellationToken = default)
    {
        await transactions.ExecuteAsync(scopeFactory, async db =>
        {
            var permission = await db.Permissions.SingleOrDefaultAsync(
                x => x.UserId == userId && x.Capability == capability, cancellationToken);

            if (permission is not null)
                permission.RevokedAt = DateTimeOffset.UtcNow;
        }, cancellationToken);
    }

    public async Task RestoreAsync(Guid userId, PermissionCapability capability, CancellationToken cancellationToken = default)
    {
        await transactions.ExecuteAsync(scopeFactory, async db =>
        {
            var permission = await db.Permissions.SingleOrDefaultAsync(
                x => x.UserId == userId && x.Capability == capability, cancellationToken);

            if (permission is not null)
                permission.RevokedAt = null;
        }, cancellationToken);
    }
}
