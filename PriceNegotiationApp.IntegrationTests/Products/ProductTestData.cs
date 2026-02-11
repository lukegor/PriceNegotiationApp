using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.IntegrationTests.Products
{
    public class ProductTestData
    {
        private readonly IAppDbContext _context;
        private readonly ProductFactory _productFactory;

        public ProductTestData(IAppDbContext context, ProductFactory productFactory)
        {
            _context = context;
            _productFactory = productFactory;
        }

        public void PopulateData()
        {
            _context.Products.RemoveRange(_context.Products.ToList());
            _context.SaveChanges();

            // 2. CREATE (Używamy fabryki!)
            var products = GetSampleProducts();
            _context.Products.AddRange(products);

            // 3. SAVE
            _context.SaveChanges();
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