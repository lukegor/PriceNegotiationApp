using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Extensions.Conversions;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Models.Input_Models;
using PriceNegotiationApp.Utility;
using PriceNegotiationApp.Utility.Custom_Exceptions;

namespace PriceNegotiationApp.Services
{
	public interface IProductService
	{
		Task<IEnumerable<Product>> GetProductsAsync();
		Task<Product> GetProductAsync(string id);
		Task<UpdateResultType> UpdateProductAsync(string id, Product product);
		Task<Product> CreateProductAsync(ProductInputModel product);
		Task<bool> DeleteProductAsync(string id);
	}

	public class ProductService: IProductService
	{
		private readonly AppDbContext _context;
		private readonly ILogger<ProductService> _logger;

		public ProductService(AppDbContext context, ILogger<ProductService> logger)
		{
			_context = context;
			_logger = logger;
		}

		public async Task<IEnumerable<Product>> GetProductsAsync()
		{
			var products = await _context.Products.ToListAsync();
			_logger.LogInformation("List of {Count} products was returned", products.Count);

			return products;
		}

		public async Task<Product> GetProductAsync(string id)
		{
			var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                _logger.LogWarning("Product with id = {Id} was not found", id);
                throw new NotFoundException();
            }

            _logger.LogInformation("Product with id = {Id} was found", product.Id);

			return product;
		}

		public async Task<UpdateResultType> UpdateProductAsync(string id, Product product)
		{
			try
			{
				var existingProduct = await GetProductAsync(id);

                var idInDb = existingProduct.Id.ToString();
                if (id != idInDb)
                {
                    _logger.LogWarning("Update failed: Provided ID {ProvidedId} does not match the ID {IdInDb} in the database.", id, idInDb);
                    return UpdateResultType.NotFound;
                }
            }
			catch (NotFoundException)
			{
                _logger.LogWarning("Update failed: Product with ID {Id} not found.", id);
                return UpdateResultType.NotFound;
            }

			_context.Entry(product).State = EntityState.Modified;

			try
			{
				await _context.SaveChangesAsync();
                _logger.LogInformation("Product with ID {Id} updated successfully.", id);
            }
			catch (DbUpdateConcurrencyException)
			{
				_logger.LogWarning("Concurrency exception occurred while updating product with ID '{Id}'", id);
				return UpdateResultType.Conflict;
			}

			return UpdateResultType.Success;
		}

		public async Task<Product> CreateProductAsync(ProductInputModel product)
		{
			Product dbProduct = product.ToDb();

			_context.Products.Add(dbProduct);
			await _context.SaveChangesAsync();

			_logger.LogInformation("Product with ID '{Id}' created successfully.", dbProduct.Id);

			return dbProduct;
		}

		public async Task<bool> DeleteProductAsync(string id)
		{
			var product = await _context.Products.FindAsync(id);
			if (product == null)
			{
                _logger.LogWarning("Product with ID '{Id}' was not found.", id);
                return false;
			}

			_context.Products.Remove(product);
			await _context.SaveChangesAsync();

            _logger.LogInformation("Product with ID {Id} was deleted successfully.", id);

            return true;
		}

		/// <summary>
		/// Checks if a product with the specified unique identifier exists.
		/// </summary>
		/// <param name="id">The unique identifier of the product to check for existence.</param>
		/// <returns>Returns true if a product with the specified ID exists; otherwise, returns false.</returns>
		public bool ProductExists(string id)
		{
			bool exists = _context.Products.Any(e => e.Id.ToString() == id);

			_logger.LogInformation(exists ? $"Product with ID '{id}' exists." : $"Product with ID '{id}' does not exist.");

			return exists;
		}
	}
}
