using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Common.Exceptions;
using PriceNegotiationApp.Application.Products.Dtos;
using PriceNegotiationApp.Application.Products.Dtos.Requests.ProductRequestDto;
using PriceNegotiationApp.Application.Products.Dtos.Responses;
using PriceNegotiationApp.Application.Products.Mappers;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.Application.Services
{
    /// <summary>
    /// <see cref="Product"/> domain service
    /// </summary>
    public interface IProductService
    {
        IQueryable<ProductDto> GetProducts();
        Task<ProductResponseDto> GetProductAsync(Guid id);
        Task<ProductResponseDto> UpdateProductAsync(Guid id, ProductRequestDto product);
        Task<ProductResponseDto> CreateProductAsync(ProductRequestDto product);
        Task DeleteProductAsync(Guid id);
    }

    /// <inheritdoc cref="IProductService"/>
    public class ProductService : IProductService
    {
        private readonly IAppDbContext _context;
        private readonly ProductFactory _productFactory;
        private readonly ILogger<ProductService> _logger;

        public ProductService(IAppDbContext context, ProductFactory productFactory, ILogger<ProductService> logger)
        {
            _context = context;
            _productFactory = productFactory;
            _logger = logger;
        }

        public IQueryable<ProductDto> GetProducts()
        {
            return _context.Products.AsNoTracking()
                .Select(x => x.ToODataResponseDto());
        }

        public async Task<ProductResponseDto> GetProductAsync(Guid id)
        {
            var product = await _context.Products.FindAsync(id) ??
                throw new NotFoundException($"Products with id = {id} was not found");

            return product.ToResponseDto();
        }

        public async Task<ProductResponseDto> UpdateProductAsync(Guid id, ProductRequestDto product)
        {
            var existingProduct = await _context.Products.FindAsync(id) ??
                throw new NotFoundException($"Products with id = {id} was not found");

            existingProduct!.Update(product.Name, new ProductPrice(product.Price));
            //_context.Entry(product).State = EntityState.Modified;

            await _context.SaveChangesAsync();
            return existingProduct.ToResponseDto();
        }

        public async Task<ProductResponseDto> CreateProductAsync(ProductRequestDto product)
        {
            Product newProduct = _productFactory.Create(product.Name, new ProductPrice(product.Price));

            _context.Products.Add(newProduct);
            await _context.SaveChangesAsync();

            return newProduct.ToResponseDto();
        }

        public async Task DeleteProductAsync(Guid id)
        {
            var product = await _context.Products.FindAsync(id) ??
                throw new NotFoundException($"Products with id = {id} was not found");

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
        }
    }
}
