using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCaching;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Services;
using PriceNegotiationApp.Utility;

namespace PriceNegotiationApp.Controllers
{
    [Area("Product")]
    [Route("api/v1/[area]/[controller]")]
    //[Produces]
    [ApiController]
    public class ProductsController : ControllerBase
    {
		private readonly ProductService _productService;

		public ProductsController(ProductService productService)
        {
			_productService = productService;
		}

		/// <summary>
		/// Retrieves a list of all products.
		/// </summary>
		/// <returns>Returns a collection of products.</returns>
		// GET: api/Products
		[HttpGet]
		[AllowAnonymous]
		[ResponseCache(Duration = 5)] //Caches the HTTP response for 5 seconds
		[ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
			var products = await _productService.GetProducts();
            return Ok(products);
        }

		/// <summary>
		/// Retrieves a specific product by its unique identifier.
		/// </summary>
		/// <param name="id">The unique identifier of the product to retrieve.</param>
		/// <returns>Returns a product with the specified ID if found; otherwise, returns a 404 Not Found response.</returns>
		// GET: api/Products/5
		[HttpGet("{id}")]
		[AllowAnonymous]
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<ActionResult<Product>> GetProduct(int id)
        {
            var product = await _productService.GetProduct(id);

			if (product == null)
            {
                return NotFound();
            }

            return Ok(product);
        }

		/// <summary>
		/// Updates a specific product by its unique identifier.
		/// </summary>
		/// <param name="id">The unique identifier of the product to update.</param>
		/// <param name="product">The updated product data.</param>
		/// <returns>
		/// Returns a 204 No Content response if the update is successful,
		/// 404 Not Found if the specified product is not found,
		/// 400 Bad Request with a message "Concurrency conflict" if a concurrency conflict occurs,
		/// or a 500 Internal Server Error for other errors.
		/// </returns>
		// PUT: api/Products/5
		// To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
		[HttpPut("{id}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status500InternalServerError)]
		public async Task<IActionResult> PutProduct(int id, [FromBody] Product product)
        {
			var updateResult = await _productService.UpdateProduct(id, product);

			return updateResult switch
			{
				UpdateResultType.Success => NoContent(),// 204 No Content
				UpdateResultType.NotFound => NotFound(),// 404 Not Found
				UpdateResultType.Conflict => BadRequest("Concurrency conflict"),// 400 Bad Request
				_ => StatusCode(500, "Internal Server Error")// Handle other errors as a generic bad request
			};
		}

		/// <summary>
		/// Creates a new product.
		/// </summary>
		/// <param name="product">The product data to create.</param>
		/// <returns>
		/// Returns a 201 Created response with the newly created product and a location header pointing to the product,
		/// or a 500 Internal Server Error if an error occurs during the creation process.
		/// </returns>
		// POST: api/Products
		// To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
		[HttpPost]
		[ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<ActionResult<Product>> PostProduct([FromBody] Product product)
        {
            await _productService.CreateProduct(product);

			return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }

		/// <summary>
		/// Deletes a specific product by its unique identifier.
		/// </summary>
		/// <param name="id">The unique identifier of the product to delete.</param>
		/// <returns>
		/// Returns a 404 Not Found response if the specified product is not found,
		/// or a 204 No Content response if the deletion is successful.
		/// </returns>
		// DELETE: api/Products/5
		[HttpDelete("{id}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> DeleteProduct(int id)
        {
			var result = await _productService.DeleteProduct(id);

			if (!result)
			{
				return NotFound();
			}

			return NoContent();
		}

		/// <summary>
		/// Checks if a product with the specified unique identifier exists.
		/// </summary>
		/// <param name="id">The unique identifier of the product to check for existence.</param>
		/// <returns>Returns true if a product with the specified ID exists; otherwise, returns false.</returns>
		private bool ProductExists(int id)
        {
            return _productService.ProductExists(id);
        }
    }
}
