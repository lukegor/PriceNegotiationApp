using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Domain.Models.Customer;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.IntegrationTests.Negotiations
{
    public class NegotiationTestData
    {
        private readonly IAppDbContext _context;
        private readonly ProductFactory _productFactory;
        private readonly NegotiationFactory _negotiationFactory;

        public NegotiationTestData(IAppDbContext context, ProductFactory productFactory, NegotiationFactory negotiationFactory)
        {
            _context = context;
            _productFactory = productFactory;
            _negotiationFactory = negotiationFactory;
        }

        public void PopulateData()
        {
            // reset
            _context.Negotiations.RemoveRange(_context.Negotiations);
            _context.Products.RemoveRange(_context.Products);
            _context.SaveChanges();

            // populate
            var products = GetSampleProducts().ToList();
            _context.Products.AddRange(products);
            _context.Negotiations.AddRange(GetSampleNegotiations(products));

            _context.SaveChanges();
        }

        private ICollection<Negotiation> GetSampleNegotiations(List<Product> products)
        {
            return new List<Negotiation>
            {
                _negotiationFactory.Create(products[0].Id, products[0].Price.Value, new ProposedPrice(4.50M), CustomerId.From(Guid.Parse("00000000-0000-0000-0000-000000000001"))),
                _negotiationFactory.Create(products[1].Id, products[1].Price.Value, new ProposedPrice(2.00M), CustomerId.From(Guid.Parse("00000000-0000-0000-0000-000000000002"))),
                _negotiationFactory.Create(products[2].Id, products[2].Price.Value, new ProposedPrice(3.00M), CustomerId.From(Guid.Parse("00000000-0000-0000-0000-000000000003"))),
            };
        }

        private ICollection<Product> GetSampleProducts()
        {
            return new List<Product>
            {
                _productFactory.Create("Demo1", new ProductPrice(5.36M)),
                _productFactory.Create("Demo2", new ProductPrice(2.36M)),
                _productFactory.Create("Demo3", new ProductPrice(3.36M)),
                _productFactory.Create("Demo4", new ProductPrice(4.36M)),
                _productFactory.Create("Demo5", new ProductPrice(5.36M))
            };
        }
    }
}