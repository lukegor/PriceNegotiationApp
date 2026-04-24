using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using PriceNegotiationApp.Application.Negotiations.Dtos;
using PriceNegotiationApp.Application.Products.Dtos;

namespace PriceNegotiationApp.Api.Extensions
{
    public static class ODataExtensions
    {
        public static IEdmModel GetEdmModel()
        {
            var builder = new ODataConventionModelBuilder();

            builder.EntityType<ProductViewModel>().HasKey(p => p.Id); // required so that OData knows what's the key
            builder.EntitySet<ProductViewModel>("Products");

            builder.EntityType<NegotiationViewModel>().HasKey(n => n.Id);
            builder.EntitySet<NegotiationViewModel>("Negotiations");
            
            builder.EnableLowerCamelCase();

            return builder.GetEdmModel();
        }
    }
}
