using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Common.Exceptions;
using PriceNegotiationApp.Application.Products.Dtos;
using PriceNegotiationApp.Application.Products.Mappers;
using PriceNegotiationApp.Application.Products.Requests.Commands;
using PriceNegotiationApp.Application.Products.Requests.Queries;
using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Application.Products
{
    /// <summary>
    /// <see cref="Product"/> service
    /// </summary>
    public interface IProductService
    {
        IQueryable<ProductViewModel> GetProducts();
        Task<ProductResultDto> GetProductAsync(GetProductByIdQuery query, CancellationToken cancellationToken);
        Task<ProductResultDto> UpdateProductAsync(UpdateProductCommand command, CancellationToken cancellationToken);
        Task<ProductResultDto> CreateProductAsync(CreateProductCommand command, CancellationToken cancellationToken);
        Task DeleteProductAsync(ProductId id, CancellationToken cancellationToken);
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

        public IQueryable<ProductViewModel> GetProducts()
        {
            return _context.Products.AsNoTracking()
                .Select(x => new ProductViewModel(
                    x.Id.Value,
                    x.Name,
                    x.Price.Value));
        }

        public async Task<ProductResultDto> GetProductAsync(GetProductByIdQuery query, CancellationToken cancellationToken)
        {
            var product = await _context.Products.FindAsync(query.Id, cancellationToken) ??
                throw new NotFoundException($"Products with id = {query.Id} was not found");

            return product.ToResultDto();
        }

        public async Task<ProductResultDto> UpdateProductAsync(UpdateProductCommand command, CancellationToken cancellationToken)
        {
            var existingProduct = await _context.Products.FindAsync(command.Id, cancellationToken) ??
                throw new NotFoundException($"Products with id = {command.Id} was not found");

            existingProduct!.Update(command.Name, command.Price);
            //_context.Entry(command).State = EntityState.Modified;

            await _context.SaveChangesAsync(cancellationToken);
            return existingProduct.ToResultDto();
        }

        public async Task<ProductResultDto> CreateProductAsync(CreateProductCommand command, CancellationToken cancellationToken)
        {
            Product newProduct = _productFactory.Create(command.Name, command.Price);

            _context.Products.Add(newProduct);
            await _context.SaveChangesAsync(cancellationToken);

            return newProduct.ToResultDto();
        }

        public async Task DeleteProductAsync(ProductId id, CancellationToken cancellationToken)
        {
            var product = await _context.Products.FindAsync(id) ??
                throw new NotFoundException($"Products with id = {id} was not found");

            _context.Products.Remove(product);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
