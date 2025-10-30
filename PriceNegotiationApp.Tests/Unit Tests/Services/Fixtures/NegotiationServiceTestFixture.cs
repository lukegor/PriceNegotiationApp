using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PriceNegotiationApp.Services.Providers;
using PriceNegotiationApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Data;

namespace PriceNegotiationApp.Tests.Unit_Tests.Services.Fixtures
{
    public class NegotiationServiceTestFixture: IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public AppDbContext DbContext { get; }
        public NegotiationService NegotiationService { get; }

        public NegotiationServiceTestFixture()
        {
            DbContext = DbContextProvider.GetInMemoryDbContext();
            NegotiationService = SetupEmptyNegotiationService();
        }

        public void Dispose()
        {
            DbContext.Dispose();
        }

        private NegotiationService SetupEmptyNegotiationService(string userId = "user2")
        {
            var context = DbContext;

            // Create a mock for IHttpContextAccessor and set up a basic behavior
            var httpContextAccessorSubstitute = CreateHttpContextAccessor(userId);

            var claimsProvider = new HttpExecutionContext(httpContextAccessorSubstitute);
            var fakeLogger = Substitute.For<ILogger<NegotiationService>>();

            return new NegotiationService(context, claimsProvider, fakeLogger);
        }

        public static IEnumerable<object[]> ProvideNegotiationData(AppDbContext dbContext)
        {
            List<object[]> negotiations = new List<object[]>
            {
                new object[] { "123ab", 1.78M, "user2" },
                new object[] { "123ac", 1.99M, "user3" },
            };

            return negotiations;
        }

        private IHttpContextAccessor CreateHttpContextAccessor(string userId)
        {
            var httpContextAccessorSubstitute = Substitute.For<IHttpContextAccessor>();
            httpContextAccessorSubstitute.HttpContext.Returns(new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, userId),
                    new Claim(ClaimTypes.NameIdentifier, userId)
                }))
            });

            return httpContextAccessorSubstitute;
        }

        //public void Dispose()
        //{
        //	_dbContext.Dispose();
        //}

        public void PopulateData(bool isCustomProductId = false)
        {
            //dbContext.Products.Load();
            //foreach (var existingProduct in dbContext.Products)
            //{
            //	_output.WriteLine($"Existing Products ID: {existingProduct.Id}, Name: {existingProduct.Name}");
            //}
            //var sampleProducts = dbContext.Products.ToList();
            //var sampleNegotiations = GetSampleNegotiations().ToList();

            //_output.WriteLine($"Sample Products Count: {sampleProducts.Count}");
            //_output.WriteLine($"Sample Negotiations Count: {sampleNegotiations.Count}");


            //// Clear existing data
            DbContext.Negotiations.RemoveRange(DbContext.Negotiations);
            DbContext.Products.RemoveRange(DbContext.Products);
            DbContext.SaveChanges();

            DbContext.Database.EnsureDeleted();
            DbContext.Database.EnsureCreated();

            // Add samples
            if (!isCustomProductId)
            {
                DbContext.Products.AddRange(GetSampleProducts());
            }
            else
            {
                DbContext.Products.AddRange(GetSampleProductsWithCustomIds());
            }
            DbContext.Negotiations.AddRange(GetSampleNegotiations());
            DbContext.SaveChanges();
        }

        private static IEnumerable<Negotiation> GetSampleNegotiations(bool isCustomProductId = false)
        {
            var sampleProducts = GetSampleProducts().ToList(); // Assuming GetSampleProducts is defined
            if (isCustomProductId)
            {
                sampleProducts = GetSampleProductsWithCustomIds().ToList();
            }

            List<Negotiation> negotiations = new List<Negotiation>
            {
                new Negotiation(sampleProducts[0].Id, 4.50M, false, "user1"),
                new Negotiation(sampleProducts[1].Id, 2.00M, false, "user2"),
                new Negotiation(sampleProducts[2].Id, 3.00M, false, "user3"),
            };

            return negotiations;
        }

        private static IEnumerable<Product> GetSampleProducts()
            => new List<Product>
            {
                new Product(
					//Id = 1,
					"Demo1",
                    new ProductPrice(5.36M) ),
                new Product(
					//Id = 2,
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

        private static IEnumerable<Product> GetSampleProductsWithCustomIds()
            => new List<Product>
            {
                new Product(
					//Id = 1,
					"Demo1",
                    new ProductPrice(5.36M) ),
                new Product(
                    "Demo2",
                    new ProductPrice(2.36M) )
                {
                    Id = "123ab",
                },
                new Product(
                    "Demo3",
                    new ProductPrice(3.36M) )
                {
                    Id = "123ac",
                },
                new Product(
					//Id = 4,
					"Demo4",
                    new ProductPrice(4.36M)),
                new Product(
					//Id = 5,
					"Demo5",
                    new ProductPrice(5.36M) )
            };
    }
}
