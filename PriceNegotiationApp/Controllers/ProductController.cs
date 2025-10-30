using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Attributes;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Domain.Models.Products.Dto;
using PriceNegotiationApp.Services;
using PriceNegotiationApp.Utility.Utility;

namespace PriceNegotiationApp.Controllers
{
	[Area("Products")]
	[Route("api/v1/[area]/[controller]")]
	//[Produces]
	[ApiController]
    public class ProductController(IProductService productService) : ControllerBase
	{
		private readonly IProductService _productService = productService;

        /// <summary>
        /// Retrieves a list of all products.
        /// </summary>
        /// <returns>Returns a collection of products.</returns>
        // GET: api/Products
        [HttpGet]
        [Route("all")]
        [EnableQuery]
        [ODataAttributeRouting]
        [AllowAnonymous]
        [ResponseCache(Duration = 5)] //Caches the HTTP response for 5 seconds
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IQueryable<Product>> GetProducts()
        {
            var products = _productService.GetProductsAsync();

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
			var product = await _productService.GetProductAsync(id);

			return Ok(product);
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
		[Authorize(Roles = "Admin, Staff")]
		public async Task<IActionResult> PutProduct([FromRoute] string id, [FromBody] ProductRequestDto product)
		{
			var errors = ModelStateHelper.GetErrors(ModelState);
			if (errors.Any())
			{
				return BadRequest(errors);
			}

			var updated = await _productService.UpdateProductAsync(id, product);

			return Ok(updated);
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
		public async Task<ActionResult<Product>> PostProduct([FromBody] ProductRequestDto product)
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
			await _productService.DeleteProductAsync(id);

			return NoContent();
		}
	}
}
