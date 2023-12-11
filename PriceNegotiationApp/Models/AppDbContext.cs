using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PriceNegotiationApp.Models
{
	public class AppDbContext : IdentityDbContext<IdentityUser>
	{
		public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
		{
		}

		public DbSet<Product> Products { get; set; } = null;
		public DbSet<Negotiation> Negotiations { get; set; } = null;
		public DbSet<ApplicationUser> ApplicationUsers { get; set; }
	}
}
