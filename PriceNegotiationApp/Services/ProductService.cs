using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Utility;

namespace PriceNegotiationApp.Services
{
	public interface IProductService
	{
		Task<IEnumerable<Product>> GetProducts();
		Task<Product> GetProduct(int id);
		Task<UpdateResultType> UpdateProduct(int id, Product product);
		Task<Product> CreateProduct(Product product);
		Task<bool> DeleteProduct(int id);
	}

	public class ProductService
	{
		private readonly AppDbContext _context;

		public ProductService(AppDbContext context)
		{
			_context = context;
		}

		public async Task<IEnumerable<Product>> GetProducts()
		{
			return await _context.Products.ToListAsync();
		}

		public async Task<Product> GetProduct(int id)
		{
			return await _context.Products.FindAsync(id);
		}

		public async Task<UpdateResultType> UpdateProduct(int id, Product product)
		{
			if (id != product.Id)
			{
				return UpdateResultType.NotFound;
			}

			_context.Entry(product).State = EntityState.Modified;

			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateConcurrencyException)
			{
				return UpdateResultType.Conflict;
			}

			return UpdateResultType.Success;
		}

		public async Task<Product> CreateProduct(Product product)
		{
			_context.Products.Add(product);
			await _context.SaveChangesAsync();

			return product;
		}

		public async Task<bool> DeleteProduct(int id)
		{
			var product = await _context.Products.FindAsync(id);
			if (product == null)
			{
				return false;
			}

			_context.Products.Remove(product);
			await _context.SaveChangesAsync();

			return true;
		}

		public bool ProductExists(int id)
		{
			return _context.Products.Any(e => e.Id == id);
		}
	}
}
