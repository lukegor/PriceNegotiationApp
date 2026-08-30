using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Modules.Identity.Infrastructure.Persistence;

namespace PriceNegotiationApp.Modules.Identity.Infrastructure.Persistence;

internal sealed class IdentityModuleDbContext(DbContextOptions<IdentityModuleDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("identity");
        // Pin snake_case names so Identity stores never depend on naming conventions.
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
    }
}

