using Bogus;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.UnitTests.Domain.Products
{
    public class ProductBuilder
    {
        private readonly Faker _faker = new("pl");

        private ProductId _id = ProductId.From(_faker.Random.Guid());
        private string _name = _faker.Commerce.ProductName();
        private ProductPrice _price = new(_faker.Finance.Amount(1, 10000000));

        public ProductBuilder()
        {
        }

        public Product Build()
        {
            return new Product(_id, _name, _price);
        }

        public ProductBuilder WithId(Guid id)
        {
            _id = ProductId.From(id);
            return this;
        }

        public ProductBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public ProductBuilder WithPrice(decimal price)
        {
            _price = new ProductPrice(price);
            return this;
        }

        public static Product Default() => new ProductBuilder().Build();
    }
}
