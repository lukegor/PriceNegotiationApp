using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Attributes;
using PriceNegotiationApp.Application.Products.Dtos;
using PriceNegotiationApp.Application.Products.Dtos.Requests.ProductRequestDto;
using PriceNegotiationApp.Application.Services;
using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Api.Controllers
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
        public ActionResult<IQueryable<ProductDto>> GetProducts()
        {
            var products = _productService.GetProducts();

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
        public async Task<ActionResult<Product>> GetProduct([FromRoute] Guid id)
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
        public async Task<IActionResult> PutProduct([FromRoute] Guid id, [FromBody] ProductRequestDto product)
        {
            var updated = await _productService.UpdateProductAsync(id, product);

            return Ok(updated);
        }

        /// <summary>
        /// Creates a new product.
        /// </summary>
        /// <param name="product">The product data to create.</param>
        /// <returns>
        /// Returns a 201 Created response with the newly created product and a location header pointing to the product,
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
            var dbProduct = await _productService.CreateProductAsync(product);

            return CreatedAtAction(nameof(GetProduct), new { id = dbProduct.Id }, dbProduct);
        }

        /// <summary>
        /// Deletes a specific product by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the product to delete.</param>
        /// <returns>
        /// a 204 No Content response if the deletion is successful.
        /// </returns>
        // DELETE: api/Products/5
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Roles = "Admin, Staff")]
        public async Task<IActionResult> DeleteProduct([FromRoute] Guid id)
        {
            await _productService.DeleteProductAsync(id);

            return NoContent();
        }
    }
}
