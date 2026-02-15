using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using PriceNegotiationApp.Contracts.Products.Dtos;

namespace PriceNegotiationApp.Api.Extensions
{
    public static class ODataExtensions
    {
        public static IEdmModel GetEdmModel()
        {
            var builder = new ODataConventionModelBuilder();

            builder.EntityType<ProductDto>().HasKey(p => p.Id); // required so that OData knows what's the key
            builder.EntitySet<ProductDto>("Products");
            builder.EnableLowerCamelCase();

            return builder.GetEdmModel();
        }
    }
}
