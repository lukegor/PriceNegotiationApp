using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Identity.Features.Auth;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Api.Endpoints.Identity.Me;

internal static class MeEndpoint
{
    internal static void MapMe(this RouteGroupBuilder group)
    {
        group.MapGet("/me", (ClaimsPrincipal principal) =>
            {
                var caller = principal.ToCallerContext();
                return TypedResults.Ok(new CurrentUserResponse(caller.UserId, caller.Email, caller.Roles.ToList()));
            })
        .WithName("GetCurrentUser")
        .WithSummary("Return the authenticated caller's profile");
    }
}
