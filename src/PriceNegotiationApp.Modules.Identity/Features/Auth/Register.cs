using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

internal static class Register
{
    internal static void MapRegister(this RouteGroupBuilder group)
    {
        group.MapPost("/register", async (RegisterRequest request,
                RegisterUserHandler handler, CancellationToken ct) =>
            TypedResults.Created("/api/v1/auth/me", await handler.HandleAsync(request)))
        .RequireRateLimiting(Policies.AuthRateLimitPolicy)
        .AllowAnonymous();
    }
}
