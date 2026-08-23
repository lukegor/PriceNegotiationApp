using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PriceNegotiationApp.Api.Contracts;
using PriceNegotiationApp.Api.Extensions;
using PriceNegotiationApp.Application.Features.Auth;

namespace PriceNegotiationApp.Api.Modules;

public static class AuthModule
{
    public static IEndpointRouteBuilder MapAuthApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/register",
                async (RegisterRequest request, IAuthService auth, CancellationToken ct) =>
                    TypedResults.Created("/api/v1/auth/me", await auth.RegisterAsync(request.Email, request.Password, ct)))
            .RequireRateLimiting(WebApplicationBuilderExtensions.AuthRateLimitPolicy)
            .AllowAnonymous();

        group.MapPost("/login",
                async (LoginRequest request, IAuthService auth, CancellationToken ct) =>
                    TypedResults.Ok(await auth.LoginAsync(request.Email, request.Password, ct)))
            .RequireRateLimiting(WebApplicationBuilderExtensions.AuthRateLimitPolicy)
            .AllowAnonymous();

        group.MapGet("/me",
                (ClaimsPrincipal principal, IAuthService auth) =>
                    TypedResults.Ok(auth.CurrentUserAsync(principal.ToCallerContext())))
            .RequireAuthorization();

        return app;
    }
}
