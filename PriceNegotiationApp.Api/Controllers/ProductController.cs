using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Attributes;
using PriceNegotiationApp.Application.Products;
using PriceNegotiationApp.Application.Products.Dtos;
using PriceNegotiationApp.Application.Products.Requests.Queries;
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
    public class ProductController(IProductService _productService) : ControllerBase
    {
        // GET: api/v1/Products/all
        [HttpGet]
        [Route("all")]
        [EnableQuery]
        [ODataAttributeRouting]
        [AllowAnonymous]
        [ResponseCache(Duration = 5)] //Caches the HTTP response for 5 seconds
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [EndpointDescription("Retrieves a list of all products.")]
        public ActionResult<IQueryable<ProductViewModel>> GetProducts()
        {
            var products = _productService.GetProducts();

            return Ok(products);
        }

        // GET: api/v1/Products/5
        [HttpGet("{id}", Name = nameof(GetProduct))]
        [AllowAnonymous]
        [EndpointDescription("Retrieves a specific product by its unique identifier.")]
        public async Task<Results<Ok<ProductResponseDto>, NotFound>> GetProduct(
            [FromRoute] ProductId id, CancellationToken cancellationToken)
        {
            var product = await _productService.GetProductAsync(new GetProductByIdQuery(id), cancellationToken);

            return TypedResults.Ok(product.ToResponseDto());
        }

        // PUT: api/v1/Products/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, Staff")]
        [EndpointDescription("Updates a specific product by its unique identifier.")]
        public async Task<Results<Ok<ProductResponseDto>, NotFound, BadRequest>> UpdateProduct(
            [FromRoute] ProductId id, [FromBody] ProductRequestDto request, CancellationToken cancellationToken)
        {
            var updated = await _productService.UpdateProductAsync(request.ToUpdateProductCommand(id), cancellationToken);

            return TypedResults.Ok(updated.ToResponseDto());
        }

        // POST: api/v1/Products
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        [Authorize(Roles = "Admin, Staff")]
        [EndpointDescription("Creates a new product.")]
        public async Task<Results<CreatedAtRoute<ProductResponseDto>, BadRequest>> CreateProduct(
            [FromBody] ProductRequestDto request, CancellationToken cancellationToken)
        {
            var dbProduct = await _productService.CreateProductAsync(request.ToCreateProductCommand(), cancellationToken);

            return TypedResults.CreatedAtRoute(dbProduct.ToResponseDto(), nameof(GetProduct), new { area = "Products", id = dbProduct.Id });
        }

        /// <summary>
        /// Deletes a specific product by its unique identifier.
        /// </summary>
        /// <returns>a 204 No Content response if the deletion is successful.</returns>
        // DELETE: api/v1/Products/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, Staff")]
        [EndpointDescription("Deletes a specific product by its unique identifier.")]
        public async Task<Results<NoContent, NotFound>> DeleteProduct([FromRoute] ProductId id, CancellationToken cancellationToken)
        {
            await _productService.DeleteProductAsync(id, cancellationToken);

            return TypedResults.NoContent();
        }
    }
}
