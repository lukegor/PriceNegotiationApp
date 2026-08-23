using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Domain.Models.Customers;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;
using Refit;
using System.Net.Http.Headers;

namespace PriceNegotiationApp.IntegrationTests
{
    public class BaseIntegrationTest : IClassFixture<IntegrationTestFactory>
    {
        protected readonly IntegrationTestFactory Factory;

        private readonly IServiceScope _scope;
        protected readonly IAppDbContext DbContext;

        private readonly RefitSettings _settings = new RefitSettings
        {
            // necessary to stop overwriting spaces in OData, acts like browser
            UrlParameterKeyFormatter = new DefaultUrlParameterKeyFormatter(),
            UrlParameterFormatter = new DefaultUrlParameterFormatter()
        };

        protected BaseIntegrationTest(IntegrationTestFactory factory)
        {
            Factory = factory;
            _scope = factory.Services.CreateScope();

            // necessary to reset Db after every test, to stop tests from interfering with one another
            Factory.ResetDatabase();

            DbContext = _scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        }

        protected T GetService<T>() where T : notnull
        {
            return _scope.ServiceProvider.GetRequiredService<T>();
        }

        protected TApi CreateGuestClient<TApi>()
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                // necessary to prevent caching responses, which can interfere with tests
                NoCache = true
            };
            return RestService.For<TApi>(client, _settings);
        }

        protected TApi CreateAuthenticatedClient<TApi>(string authPayload)
        {
            // Client with a request-intercepting handler
            var client = Factory.CreateDefaultClient(new TestAuthHeaderHandler(authPayload));
            client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
            {
                // necessary to prevent caching responses, which can interfere with tests
                NoCache = true
            };
            return RestService.For<TApi>(client, _settings);
        }

        protected Product SeedProduct(string name, decimal price)
        {
            var product = GetService<ProductFactory>().Create(
                name,
                new ProductPrice(price));

            DbContext.Products.Add(product);
            DbContext.SaveChanges();

            return product;
        }

        protected Customer SeedCustomer(Guid id, string name)
        {
            var customer = GetService<CustomerFactory>().Create(
                id,
                name);

            DbContext.Customers.Add(customer);
            DbContext.SaveChanges();

            return customer;
        }

        protected Negotiation SeedNegotiation(
            ProductId productId,
            decimal originalPrice,
            decimal proposedPrice,
            Guid customerId)
        {
            var negotiation = GetService<NegotiationFactory>().Create(
                productId,
                originalPrice,
                new ProposedPrice(proposedPrice),
                CustomerId.From(customerId)
            );

            DbContext.Negotiations.Add(negotiation);
            DbContext.SaveChanges();

            return negotiation;
        }

        protected TApi GetClientForRole<TApi>(string role) => role switch
        {
            "Admin" => AsAdmin<TApi>(),
            "Staff" => AsStaff<TApi>(),
            "User" => AsUser<TApi>(),
            _ => AsGuest<TApi>()
        };

        // Facades
        protected TApi AsGuest<TApi>() => CreateGuestClient<TApi>();
        protected TApi AsAdmin<TApi>() => CreateAuthenticatedClient<TApi>(TestIdentities.AdminPayload);
        protected TApi AsStaff<TApi>() => CreateAuthenticatedClient<TApi>(TestIdentities.StaffPayload);
        protected TApi AsUser<TApi>() => CreateAuthenticatedClient<TApi>(TestIdentities.UserPayload);

        // Facade for dynamic actors
        protected TApi AsUser<TApi>(Guid userId) =>
            CreateAuthenticatedClient<TApi>(TestIdentities.BuildDynamic(userId, "User"));
    }
}
