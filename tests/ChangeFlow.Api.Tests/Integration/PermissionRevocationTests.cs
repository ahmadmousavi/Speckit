using ChangeFlow.Api.Controllers;
using ChangeFlow.Api.Data;
using ChangeFlow.Api.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeFlow.Api.Tests.Integration;

public sealed class PermissionRevocationTests : IClassFixture<ChangeFlowWebApplicationFactory>
{
    private readonly ChangeFlowWebApplicationFactory _factory;

    public PermissionRevocationTests(ChangeFlowWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Revoked_permission_is_inactive_and_restoration_reactivates_it()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ChangeFlowDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<PermissionStore>();

        await store.RevokeAsync(DevelopmentSeed.RequesterOnlyId, PermissionCapability.Requester);

        var revoked = await db.Permissions.FindAsync(
            DevelopmentSeed.RequesterOnlyId,
            PermissionCapability.Requester);
        Assert.NotNull(revoked);
        Assert.NotNull(revoked.RevokedAt);

        await store.RestoreAsync(DevelopmentSeed.RequesterOnlyId, PermissionCapability.Requester);

        db.ChangeTracker.Clear();

        var restored = await db.Permissions.FindAsync(
            DevelopmentSeed.RequesterOnlyId,
            PermissionCapability.Requester);
        Assert.NotNull(restored);
        Assert.Null(restored.RevokedAt);
    }

    [Fact]
    public async Task Mutation_commits_before_later_revocation()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ChangeFlowDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<PermissionStore>();

        var permission = await db.Permissions.FindAsync(
            DevelopmentSeed.RequesterOnlyId,
            PermissionCapability.Requester);
        Assert.NotNull(permission);

        var mutationCompleted = false;
        await using (var mutationScope = _factory.Services.CreateAsyncScope())
        {
            var mutationDb = mutationScope.ServiceProvider.GetRequiredService<ChangeFlowDbContext>();
            var row = await mutationDb.Permissions.FindAsync(
                DevelopmentSeed.RequesterOnlyId,
                PermissionCapability.Requester);
            Assert.NotNull(row);
            mutationCompleted = row.RevokedAt is null;
        }

        await store.RevokeAsync(DevelopmentSeed.RequesterOnlyId, PermissionCapability.Requester);

        Assert.True(mutationCompleted);
        db.ChangeTracker.Clear();
        var revoked = await db.Permissions.FindAsync(
            DevelopmentSeed.RequesterOnlyId,
            PermissionCapability.Requester);
        Assert.NotNull(revoked?.RevokedAt);
    }

    [Fact]
    public async Task Revocation_committed_before_mutation_rejects_permission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<PermissionStore>();

        await store.RevokeAsync(DevelopmentSeed.RequesterOnlyId, PermissionCapability.Requester);

        var permissionStoreDb = scope.ServiceProvider.GetRequiredService<ChangeFlowDbContext>();
        permissionStoreDb.ChangeTracker.Clear();
        var permission = await permissionStoreDb.Permissions.FindAsync(
            DevelopmentSeed.RequesterOnlyId,
            PermissionCapability.Requester);

        Assert.NotNull(permission);
        Assert.NotNull(permission.RevokedAt);
    }
}
