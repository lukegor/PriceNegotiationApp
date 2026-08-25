using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

internal static class Login
{
    internal static void MapLogin(this RouteGroupBuilder group)
    {
        group.MapPost("/login", async (LoginRequest request, LoginUserHandler handler,
                CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(request)))
        .RequireRateLimiting(Policies.AuthRateLimitPolicy)
        .AllowAnonymous();
    }
}
