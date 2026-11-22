using ChangeFlow.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChangeFlow.Api.Data;

public sealed class ChangeFlowDbContext(DbContextOptions<ChangeFlowDbContext> options) : DbContext(options)
{
    public DbSet<ChangeRequest> ChangeRequests => Set<ChangeRequest>();
    public DbSet<UserIdentity> UserIdentities => Set<UserIdentity>();
    public DbSet<Permission> Permissions => Set<Permission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChangeRequest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Title).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Description).IsRequired();
            entity.Property(x => x.Reason).IsRequired();
            entity.Property(x => x.LastChangeRequestComment);
            entity.Property(x => x.RequesterId).IsRequired();
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.ConcurrencyToken).IsRequired().IsConcurrencyToken();
        });

        modelBuilder.Entity<UserIdentity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(x => new { x.UserId, x.Capability });
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.Capability).IsRequired();
            entity.Property(x => x.RevokedAt);
            entity.HasIndex(x => new { x.UserId, x.Capability }).IsUnique();
        });
    }
}
