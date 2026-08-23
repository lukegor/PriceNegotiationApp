using Microsoft.AspNetCore.Mvc;
using PriceNegotiationApp.Api.Contracts;
using PriceNegotiationApp.Api.Extensions;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Features.Negotiations;
using System.Security.Claims;

namespace PriceNegotiationApp.Api.Modules;

public static class NegotiationsModule
{
    public static IEndpointRouteBuilder MapNegotiationsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/negotiations").WithTags("Negotiations");

        group.MapPost("/",
                async (CreateNegotiationRequest request, ClaimsPrincipal principal, INegotiationService negotiations,
                    CancellationToken ct) =>
                    TypedResults.Created("/api/v1/negotiations/mine",
                        await negotiations.CreateAsync(principal.ToCallerContext(), request.ProductId, request.ProposedPrice, ct)))
            .RequireRoles(UserRoles.Customer);

        group.MapGet("/mine",
                async (ClaimsPrincipal principal, INegotiationService negotiations, CancellationToken ct,
                    [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
                    TypedResults.Ok(await negotiations.ListMineAsync(
                        principal.ToCallerContext(), new PageQuery(page, pageSize), ct)))
            .RequireRoles(UserRoles.Customer);

        group.MapGet("/",
                async (INegotiationService negotiations, CancellationToken ct,
                    [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
                    TypedResults.Ok(await negotiations.ListAsync(new PageQuery(page, pageSize), ct)))
            .RequireRoles(UserRoles.Admin, UserRoles.Staff);

        group.MapGet("/{id:guid}",
                async (Guid id, ClaimsPrincipal principal, INegotiationService negotiations, CancellationToken ct) =>
                    TypedResults.Ok(await negotiations.GetAsync(principal.ToCallerContext(), id, ct)))
            .RequireAuthorization();

        group.MapPatch("/{id:guid}/proposals",
                async (Guid id, CounterProposalRequest request, ClaimsPrincipal principal,
                    INegotiationService negotiations, CancellationToken ct) =>
                    TypedResults.Ok(await negotiations.CounterProposeAsync(
                        principal.ToCallerContext(), id, request.ProposedPrice, ct)))
            .RequireAuthorization();

        group.MapPost("/{id:guid}/accept",
                async (Guid id, INegotiationService negotiations, CancellationToken ct) =>
                    TypedResults.Ok(await negotiations.AcceptAsync(id, ct)))
            .RequireRoles(UserRoles.Admin, UserRoles.Staff);

        group.MapPost("/{id:guid}/decline",
                async (Guid id, INegotiationService negotiations, CancellationToken ct) =>
                    TypedResults.Ok(await negotiations.DeclineAsync(id, ct)))
            .RequireRoles(UserRoles.Admin, UserRoles.Staff);

        group.MapDelete("/{id:guid}",
                async (Guid id, ClaimsPrincipal principal, INegotiationService negotiations, CancellationToken ct) =>
                {
                    await negotiations.WithdrawAsync(principal.ToCallerContext(), id, ct);
                    return TypedResults.NoContent();
                })
            .RequireAuthorization();

        return app;
    }
}

