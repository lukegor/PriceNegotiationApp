using Microsoft.EntityFrameworkCore;

namespace PriceNegotiationApp.Models
{
	public class AppDbContext : DbContext
	{
		public AppDbContext(DbContextOptions options) : base(options)
		{
		}

		public DbSet<Product> Products { get; set; } = null;
		public DbSet<Negotiation> Negotiations { get; set; } = null;
	}
}
