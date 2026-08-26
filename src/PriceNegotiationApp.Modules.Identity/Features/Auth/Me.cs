using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

internal static class Me
{
    internal static void MapMe(this RouteGroupBuilder group)
    {
        group.MapGet("/me", (ClaimsPrincipal principal) =>
            {
                var caller = principal.ToCallerContext();
                return TypedResults.Ok(new CurrentUserResponse(caller.UserId, caller.Email, caller.Roles.ToList()));
            });
    }
}
