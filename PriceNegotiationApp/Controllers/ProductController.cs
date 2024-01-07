using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PriceNegotiationApp.Extensions;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Models.Input_Models;
using PriceNegotiationApp.Services;
using PriceNegotiationApp.Utility;
using PriceNegotiationApp.Utility.Custom_Exceptions;

namespace PriceNegotiationApp.Controllers
{
	[Area("Products")]
	[Route("api/v1/[area]/[controller]")]
	//[Produces]
	[ApiController]
	public class ProductController : ControllerBase
	{
		private readonly IProductService _productService;

		public ProductController(IProductService productService)
		{
			_productService = productService;
		}

		/// <summary>
		/// Retrieves a list of all products.
		/// </summary>
		/// <returns>Returns a collection of products.</returns>
		// GET: api/Products
		[HttpGet]
		[Route("all")]
		[AllowAnonymous]
		[ResponseCache(Duration = 5)] //Caches the HTTP response for 5 seconds
		[ProducesResponseType(StatusCodes.Status200OK)]
		public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
		{
			var products = await _productService.GetProductsAsync();

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
		public async Task<ActionResult<Product>> GetProduct([FromRoute] string id)
		{
			try
			{
				var product = await _productService.GetProductAsync(id);

				return Ok(product);
			}
			catch (NotFoundException)
			{
				return NotFound();
			}
		}

        /// <summary>
        /// Updates a specific product by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the product to update.</param>
        /// <param name="product">The updated product data.</param>
        /// <returns>
        /// Returns a 204 No Content response if the update is successful,
        /// a 400 Bad Request if a the model state is invalid,
        /// a 403 Forbidden if the user is not authorized or does not possess the required role,
        /// a 404 Not Found if the specified product is not found,
        /// a 409 Conflict if a concurrency conflict occurs in database,
        /// or a 500 Internal Server Error for other errors.
        /// </returns>
        // PUT: api/Products/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
		[Authorize(Roles = "Admin, Staff")]
		public async Task<IActionResult> PutProduct([FromRoute] string id, [FromBody] Product product)
		{
			var errors = ModelStateHelper.GetErrors(ModelState);
			if (errors.Any())
			{
				return BadRequest(errors);
			}

			var updateResult = await _productService.UpdateProductAsync(id, product);

			return updateResult switch
			{
				UpdateResultType.Success => NoContent(),// 204 No Content
				UpdateResultType.NotFound => NotFound(),// 404 Not Found
				UpdateResultType.Conflict => Conflict("Concurrency conflict"),// 409 Conflict
				_ => StatusCode(500, "Internal Server Error")// Handle other errors as a generic bad request
			};
		}

		/// <summary>
		/// Creates a new product.
		/// </summary>
		/// <param name="product">The product data to create.</param>
		/// <returns>
		/// Returns a 201 Created response with the newly created product and a location header pointing to the product,
		/// a 400 Bad Request response if the model state is invalid
		/// a 403 Forbidden response if the user is not authorized or does not possess the required role,
		/// or a 500 Internal Server Error if an error occurs during the creation process.
		/// </returns>
		// POST: api/Products
		// To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
		[HttpPost]
		[ProducesResponseType(StatusCodes.Status201Created)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[Authorize(Roles = "Admin, Staff")]
		public async Task<ActionResult<Product>> PostProduct([FromBody] ProductInputModel product)
		{
			var errors = ModelStateHelper.GetErrors(ModelState);
			if (errors.Any())
			{
				return BadRequest(errors);
			}

			var dbProduct = await _productService.CreateProductAsync(product);

			return CreatedAtAction(nameof(GetProduct), new { id = dbProduct.Id }, dbProduct);
		}

		/// <summary>
		/// Deletes a specific product by its unique identifier.
		/// </summary>
		/// <param name="id">The unique identifier of the product to delete.</param>
		/// <returns>
		/// Returns a 404 Not Found response if the specified product is not found,
		/// or a 403 Forbidden if the user is not authorized or does not possess the required role,
		/// or a 204 No Content response if the deletion is successful.
		/// </returns>
		// DELETE: api/Products/5
		[HttpDelete("{id}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[Authorize(Roles = "Admin, Staff")]
		public async Task<IActionResult> DeleteProduct([FromRoute] string id)
		{
			var result = await _productService.DeleteProductAsync(id);

			if (!result)
			{
				return NotFound();
			}

			return NoContent();
		}
	}
}
