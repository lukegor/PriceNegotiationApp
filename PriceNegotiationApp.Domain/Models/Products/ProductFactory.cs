using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.Domain.Models.Products
{
    public class ProductFactory
    {
        private readonly IIdGenerator _idGenerator;

        public ProductFactory(IIdGenerator idGenerator)
        {
            _idGenerator = idGenerator;
        }

        public Product Create(string name, ProductPrice price)
        {
            return new Product(ProductId.From(_idGenerator.NewId()), name, price);
        }
    }
}
