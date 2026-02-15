using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.IntegrationTests.Products;
using Refit;

namespace PriceNegotiationApp.IntegrationTests
{
    public class BaseIntegrationTest : IClassFixture<IntegrationTestFactory>
    {
        private readonly IServiceScope _scope;
        protected readonly IAppDbContext DbContext;

        protected readonly HttpClient Client;
        protected readonly IProductsApi ProductsClient;

        protected BaseIntegrationTest(IntegrationTestFactory factory)
        {
            _scope = factory.Services.CreateScope();

            // reset Db after every test, to stop tests from interfering with one another
            factory.ResetDatabase();

            DbContext = _scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            Client = factory.CreateClient();

            var settings = new RefitSettings
            {
                // necessary to stop overwriting spaces in OData, acts like browser
                UrlParameterKeyFormatter = new DefaultUrlParameterKeyFormatter(),
                UrlParameterFormatter = new DefaultUrlParameterFormatter()
            };

            ProductsClient = RestService.For<IProductsApi>(Client, settings);
        }

        protected T GetService<T>() where T : notnull
        {
            return _scope.ServiceProvider.GetRequiredService<T>();
        }
    }
}
