using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;

namespace PriceNegotiationApp.BuildingBlocks;

public static class EndpointConventionExtensions
{
    public static TBuilder RequireRoles<TBuilder>(this TBuilder builder, params string[] roles)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(new AuthorizeAttribute { Roles = string.Join(',', roles) });
}
