using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PriceNegotiationApp.Models
{
	public class AppDbContext : IdentityDbContext<IdentityUser>
	{
        private readonly IWebHostEnvironment _environment;

        public AppDbContext(DbContextOptions<AppDbContext> options, IWebHostEnvironment environment) : base(options)
		{
            _environment = environment;
		}

		public DbSet<Product> Products { get; set; } = null;
		public DbSet<Negotiation> Negotiations { get; set; } = null;
		public DbSet<ApplicationUser> ApplicationUsers { get; set; }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (_environment.IsDevelopment())
            {
                optionsBuilder.EnableSensitiveDataLogging();
                optionsBuilder.EnableDetailedErrors();
            }
        }

        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
        //    base.OnModelCreating(modelBuilder);

        //    // Configure your entity relationships and other model configurations here
        //    // For example:
        //    modelBuilder.Entity<Negotiation>()
        //        .HasOne(n => n.Product)
        //        .WithMany(p => p.Negotiations)
        //        .HasForeignKey(n => n.ProductId);
        //}

    }
}
