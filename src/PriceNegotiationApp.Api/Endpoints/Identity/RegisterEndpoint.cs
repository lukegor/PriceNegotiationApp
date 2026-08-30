using PriceNegotiationApp.Modules.Identity.Infrastructure.Register;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Api;
using PriceNegotiationApp.Modules.Identity.Application.Register;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Api.Endpoints.Identity.Register;

internal static class RegisterEndpoint
{
    internal static void MapRegister(this RouteGroupBuilder group)
    {
        group.MapPost("/register", async (RegisterRequest request,
                RegisterUserHandler handler, CancellationToken ct) =>
            TypedResults.Created("/api/v1/auth/me", await handler.HandleAsync(request)))
        .AddEndpointFilter<ValidateRequestFilter<RegisterRequest>>()
        .RequireRateLimiting(Policies.AuthRateLimitPolicy)
        .AllowAnonymous()
        .WithName("RegisterUser")
        .WithSummary("Register a new customer account")
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
