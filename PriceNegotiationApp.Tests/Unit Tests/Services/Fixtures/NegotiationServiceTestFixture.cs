using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Services.Providers;
using PriceNegotiationApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace PriceNegotiationApp.Tests.Unit_Tests.Services.Fixtures
{
    public class NegotiationServiceTestFixture: IDisposable
    {
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

            var claimsProvider = new HttpContextClaimsProvider(httpContextAccessorSubstitute);
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
            //	_output.WriteLine($"Existing Product ID: {existingProduct.Id}, Name: {existingProduct.Name}");
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
                new Negotiation(sampleProducts[0].Id, 4.50M, "user1"),
                new Negotiation(sampleProducts[1].Id, 2.00M, "user2"),
                new Negotiation(sampleProducts[2].Id, 3.00M, "user3"),
            };

            return negotiations;
        }

        private static IEnumerable<Product> GetSampleProducts()
            => new List<Product>
            {
                new Product{
					//Id = 1,
					Name = "Demo1",
                    Price = 5.36M },
                new Product{
					//Id = 2,
					Name = "Demo2",
                    Price = 2.36M },
                new Product{
					//Id = 3,
					Name = "Demo3",
                    Price = 3.36M },
                new Product{
					//Id = 4,
					Name = "Demo4",
                    Price = 4.36M },
                new Product{
					//Id = 5,
					Name = "Demo5",
                    Price = 5.36M }
            };

        private static IEnumerable<Product> GetSampleProductsWithCustomIds()
            => new List<Product>
            {
                new Product{
					//Id = 1,
					Name = "Demo1",
                    Price = 5.36M },
                new Product{
                    Id = "123ab",
                    Name = "Demo2",
                    Price = 2.36M },
                new Product{
                    Id = "123ac",
                    Name = "Demo3",
                    Price = 3.36M },
                new Product{
					//Id = 4,
					Name = "Demo4",
                    Price = 4.36M },
                new Product{
					//Id = 5,
					Name = "Demo5",
                    Price = 5.36M }
            };
    }
}
