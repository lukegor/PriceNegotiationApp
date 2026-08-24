using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Identity.Features.Auth;

namespace PriceNegotiationApp.Modules.Identity;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");
        group.MapRegister();
        group.MapLogin();
        group.MapMe();
        return app;
    }
}
