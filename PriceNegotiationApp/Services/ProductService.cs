using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Data;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Domain.Models.Products.Dto;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;
using PriceNegotiationApp.Utility;
using PriceNegotiationApp.Domain.Models.Mappers;
using PriceNegotiationApp.Utility.Utility.Exceptions;

namespace PriceNegotiationApp.Services
{
    /// <summary>
    /// <see cref="Product"/> domain service
    /// </summary>
    public interface IProductService
	{
		IQueryable<Product> GetProductsAsync();
		Task<Product> GetProductAsync(string id);
		Task<Product> UpdateProductAsync(string id, ProductRequestDto product);
		Task<Product> CreateProductAsync(ProductRequestDto product);
		Task DeleteProductAsync(string id);
	}

    /// <inheritdoc cref="IProductService"/>
    public class ProductService: IProductService
	{
		private readonly AppDbContext _context;
		private readonly ILogger<ProductService> _logger;

		public ProductService(AppDbContext context, ILogger<ProductService> logger)
		{
			_context = context;
			_logger = logger;
		}

		public IQueryable<Product> GetProductsAsync()
		{
			return _context.Products.AsNoTracking();
		}

		public async Task<Product> GetProductAsync(string id)
		{
			var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                throw new NotFoundException($"Products with id = {id} was not found");
            }

			return product;
		}

		public async Task<Product> UpdateProductAsync(string id, ProductRequestDto product)
		{
			var existingProduct = await _context.Products.FindAsync(id);

            if (product == null)
            {
                throw new NotFoundException($"Products with id = {id} was not found");
            }

            existingProduct.Update(product.Name, new ProductPrice(product.Price));
            //_context.Entry(product).State = EntityState.Modified;

            await _context.SaveChangesAsync();
			return existingProduct;
		}

		public async Task<Product> CreateProductAsync(ProductRequestDto product)
		{
			Product newProduct = product.ToProduct();

			_context.Products.Add(newProduct);
			await _context.SaveChangesAsync();

			return newProduct;
		}

		public async Task DeleteProductAsync(string id)
		{
			var product = await _context.Products.FindAsync(id);
			if (product == null)
			{
                throw new NotFoundException($"Products with id = {id} was not found");
            }

			_context.Products.Remove(product);
			await _context.SaveChangesAsync();
		}

		/// <summary>
		/// Checks if a product with the specified unique identifier exists.
		/// </summary>
		/// <param name="id">The unique identifier of the product to check for existence.</param>
		/// <returns>Returns true if a product with the specified ID exists; otherwise, returns false.</returns>
		public bool ProductExists(string id)
		{
			bool exists = _context.Products.Any(e => e.Id.ToString() == id);

			_logger.LogInformation(exists ? $"Products with ID '{id}' exists." : $"Products with ID '{id}' does not exist.");

			return exists;
		}
	}
}
