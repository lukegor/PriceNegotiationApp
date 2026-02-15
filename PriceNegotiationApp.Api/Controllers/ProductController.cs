using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Attributes;
using PriceNegotiationApp.Application.Products;
using PriceNegotiationApp.Application.Products.Requests.Queries;
using PriceNegotiationApp.Contracts.Products.Dtos;
using PriceNegotiationApp.Contracts.Products.Dtos.Requests;
using PriceNegotiationApp.Contracts.Products.Dtos.Responses;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Presentation.Products.Mappers;

namespace PriceNegotiationApp.Api.Controllers
{
    [Area("Products")]
    [Route("api/v1/[area]")]
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
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<IQueryable<ProductDto>> GetProducts()
        {
            var products = _productService.GetProducts();

            return Ok(products.Select(x => x.ToDto()));
        }

        /// <summary>
        /// Retrieves a specific product by its unique identifier.
        /// </summary>
        /// <returns>Returns a product with the specified ID if found.</returns>
        // GET: api/Products/5
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<Results<Ok<ProductResponseDto>, NotFound>> GetProduct([FromRoute] ProductId id)
        {
            var product = await _productService.GetProductAsync(new GetProductByIdQuery(id));

            return TypedResults.Ok(product.ToResponseDto());
        }

        /// <summary>
        /// Updates a specific product by its unique identifier.
        /// </summary>
        /// <returns>Returns a 204 No Content response if the update is successful.</returns>
        // PUT: api/Products/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, Staff")]
        public async Task<Results<Ok<ProductResponseDto>, NotFound, BadRequest>> PutProduct(
            [FromRoute] ProductId id, [FromBody] ProductRequestDto request)
        {
            var updated = await _productService.UpdateProductAsync(request.ToUpdateProductCommand(id));

            return TypedResults.Ok(updated.ToResponseDto());
        }

        /// <summary>
        /// Creates a new product.
        /// </summary>
        /// <returns>Returns a 201 Created response with the newly created product and a location header pointing to the product,/// </returns>
        // POST: api/Products
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        [Authorize(Roles = "Admin, Staff")]
        public async Task<Results<CreatedAtRoute<ProductResponseDto>, BadRequest>> PostProduct(
            [FromBody] ProductRequestDto request)
        {
            var dbProduct = await _productService.CreateProductAsync(request.ToCreateProductCommand());

            return TypedResults.CreatedAtRoute(dbProduct.ToResponseDto(), nameof(GetProduct), new { id = dbProduct.Id });
        }

        /// <summary>
        /// Deletes a specific product by its unique identifier.
        /// </summary>
        /// <returns>a 204 No Content response if the deletion is successful.</returns>
        // DELETE: api/Products/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, Staff")]
        public async Task<Results<NoContent, NotFound>> DeleteProduct([FromRoute] ProductId id)
        {
            await _productService.DeleteProductAsync(id);

            return TypedResults.NoContent();
        }
    }
}
