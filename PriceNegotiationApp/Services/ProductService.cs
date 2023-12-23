﻿using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Extensions.Conversions;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Models.Input_Models;
using PriceNegotiationApp.Utility;

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

		public ProductService(AppDbContext context)
		{
			_context = context;
		}

		public async Task<IEnumerable<Product>> GetProductsAsync()
		{
			return await _context.Products.ToListAsync();
		}

		public async Task<Product> GetProductAsync(string id)
		{
			return await _context.Products.FindAsync(id);
		}

		public async Task<UpdateResultType> UpdateProductAsync(string id, Product product)
		{
			var idInDb = (await GetProductAsync(id)).Id.ToString();
			if (id != idInDb)
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

		public async Task<Product> CreateProductAsync(ProductInputModel product)
		{
			Product dbProduct = product.ToDb();

			_context.Products.Add(dbProduct);
			await _context.SaveChangesAsync();

			return dbProduct;
		}

		public async Task<bool> DeleteProductAsync(string id)
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

		/// <summary>
		/// Checks if a product with the specified unique identifier exists.
		/// </summary>
		/// <param name="id">The unique identifier of the product to check for existence.</param>
		/// <returns>Returns true if a product with the specified ID exists; otherwise, returns false.</returns>
		public bool ProductExists(string id)
		{
			return _context.Products.Any(e => e.Id.ToString() == id);
		}
	}
}
