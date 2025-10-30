using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PriceNegotiationApp.Data;
using PriceNegotiationApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceNegotiationApp.Tests.Unit_Tests.Services.Fixtures
{
    public class ProductServiceTestFixture : IDisposable
    {
        public AppDbContext DbContext { get; }
        public ProductService ProductService { get; }

        public ProductServiceTestFixture()
        {
            DbContext = DbContextProvider.GetInMemoryDbContext();
            ProductService = SetUpEmptyProductService();
        }

        public void Dispose()
        {
            DbContext.Dispose();
        }

        private ProductService SetUpEmptyProductService()
        {
            var fakeLogger = Substitute.For<ILogger<ProductService>>();
            return new ProductService(DbContext, fakeLogger);

        }

        internal void PopulateData(bool isCustomGuid = true)
        {
            // Clear existing data
            DbContext.Products.RemoveRange(DbContext.Products);
            DbContext.SaveChanges();

            // Ensure a clean database state
            //dbContext.Database.EnsureDeleted();
            //dbContext.Database.EnsureCreated();

            // Add sample products
            DbContext.Products.AddRange(GetSampleProducts(isCustomGuid));
            DbContext.SaveChanges();
        }

        internal static IEnumerable<Product> GetSampleProducts(bool isCustomGuid = true)
        {
            List<Product> products = new List<Product>
            {
                new Product(
					//Id = "123abc",
					"Demo1",
                    new ProductPrice(5.36M) ),
                new Product(
					//Id = "123abc",
					"Demo2",
                    new ProductPrice(2.36M) ),
                new Product(
					//Id = 3,
					"Demo3",
                    new ProductPrice(3.36M) ),
                new Product(
					//Id = 4,
					"Demo4",
                    new ProductPrice(4.36M) ),
                new Product(
					//Id = 5,
					"Demo5",
                    new ProductPrice(5.36M) )
            };

            if (isCustomGuid)
            {
                products[1].Id = "123abc";
            }

            return products;
        }
    }
}
