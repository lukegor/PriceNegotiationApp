using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Api.Endpoints.Identity.Login;
using PriceNegotiationApp.Api.Endpoints.Identity.Me;
using PriceNegotiationApp.Api.Endpoints.Identity.Register;

namespace PriceNegotiationApp.Api.Endpoints.Identity;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Auth")
            .RequireAuthorization();
        group.MapRegister();
        group.MapLogin();
        group.MapMe();
        return app;
    }
}
