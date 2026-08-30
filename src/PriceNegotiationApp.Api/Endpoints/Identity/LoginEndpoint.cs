using PriceNegotiationApp.Modules.Identity.Infrastructure.Login;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Api;
using PriceNegotiationApp.Modules.Identity.Application.Login;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Api.Endpoints.Identity.Login;

internal static class LoginEndpoint
{
    internal static void MapLogin(this RouteGroupBuilder group)
    {
        group.MapPost("/login", async (LoginRequest request, LoginUserHandler handler,
                CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(request)))
        .AddEndpointFilter<ValidateRequestFilter<LoginRequest>>()
        .RequireRateLimiting(Policies.AuthRateLimitPolicy)
        .AllowAnonymous()
        .WithName("Login")
        .WithSummary("Authenticate and issue an access token")
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
