using PriceNegotiationApp.Domain.Models.Abstract;
using PriceNegotiationApp.Domain.Models.Products.Rules;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.Domain.Models.Products
{
    public class Product : Entity<ProductId>
    {
        /// <summary>
        /// Product name
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Product price
        /// </summary>
        public ProductPrice Price { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        /// <summary>
        /// Empty constructor for EF.
        /// </summary>
        private Product() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        public Product(ProductId productId, string name, ProductPrice price)
        {
            CheckRule(new ProductNameCannotBeNullOrEmptyRule(name));

            Id = productId;
            Name = name;
            Price = price;
        }

        public void Update(string name, ProductPrice price)
        {
            if (!HasChanges(name, price))
            {
                throw new DomainException("No changes detected");
            }

            CheckRule(new ProductNameCannotBeNullOrEmptyRule(name));

            Name = name;
            Price = price;
        }

        public bool HasChanges(string name, ProductPrice price)
        {
            return !string.Equals(Name, name, StringComparison.Ordinal)
                || Price != price;
        }
    }
}
