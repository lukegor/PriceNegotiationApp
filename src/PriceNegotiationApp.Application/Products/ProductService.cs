using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Common.Exceptions;
using PriceNegotiationApp.Application.Products.Dtos;
using PriceNegotiationApp.Application.Products.Mappers;
using PriceNegotiationApp.Application.Products.Requests.Commands;
using PriceNegotiationApp.Application.Products.Requests.Queries;
using PriceNegotiationApp.Domain.Models.Products;
using System.Globalization;

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

        public ProductService(IAppDbContext context, ProductFactory productFactory)
        {
            _context = context;
            _productFactory = productFactory;
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
            var product = await _context.Products.FindAsync(new object[] { query.Id }, cancellationToken) ??
                throw new NotFoundException(string.Create(CultureInfo.InvariantCulture, $"Products with id = {query.Id} was not found"));

            return product.ToResultDto();
        }

        public async Task<ProductResultDto> UpdateProductAsync(UpdateProductCommand command, CancellationToken cancellationToken)
        {
            var existingProduct = await _context.Products.FindAsync(new object[] { command.Id }, cancellationToken) ??
                throw new NotFoundException(string.Create(CultureInfo.InvariantCulture, $"Products with id = {command.Id} was not found"));

            existingProduct!.Update(command.Name, command.Price);

#pragma warning disable S125 // Sections of code should not be commented out
            //_context.Entry(command).State = EntityState.Modified;
#pragma warning restore S125 // Sections of code should not be commented out

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
            var product = await _context.Products.FindAsync(new object[] { id }, cancellationToken) ??
                throw new NotFoundException(string.Create(CultureInfo.InvariantCulture, $"Products with id = {id} was not found"));

            _context.Products.Remove(product);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
