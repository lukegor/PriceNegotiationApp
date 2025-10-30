using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Domain.Models.Users;
using PriceNegotiationApp.Infrastructure.DbEntityConfigurations;

namespace PriceNegotiationApp.Data
{
    public class AppDbContext : IdentityDbContext<IdentityUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Negotiation> Negotiations { get; set; } = null!;
        public DbSet<ApplicationUser> ApplicationUsers { get; set; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="modelBuilder"></param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new ProductConfiguration());
            modelBuilder.ApplyConfiguration(new NegotiationConfiguration());
        }

    }
}
