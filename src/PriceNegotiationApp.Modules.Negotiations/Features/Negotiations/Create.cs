using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using PriceNegotiationApp.Modules.Negotiations.Ports;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class Create
{
    internal static void MapCreate(this RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateNegotiationRequest request, ClaimsPrincipal principal,
                NegotiationsDbContext db, IProductPriceProvider products, INegotiationPolicy policy,
                TimeProvider clock, CancellationToken ct) =>
            {
                var caller = principal.ToCallerContext();
                var snapshot = await products.GetAsync(request.ProductId, ct)
                               ?? throw new NotFoundException("Product", request.ProductId);

                if (await NegotiationAccess.FindOpenAsync(db, snapshot.ProductId, caller.UserId, ct) is not null)
                {
                    throw new ConflictException(NegotiationErrorCodes.NegotiationAlreadyOpen,
                        "An open negotiation already exists for this product.");
                }

                var customerId = await NegotiationAccess.GetOrCreateCustomerIdAsync(db, caller.UserId, ct);
                var negotiation = Negotiation.Start(customerId, snapshot.ProductId, snapshot.Price,
                    request.ProposedPrice, clock.GetUtcNow(), policy);
                await db.Negotiations.AddAsync(negotiation, ct);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created("/api/v1/negotiations/mine",
                    NegotiationResponses.ToResponse(negotiation, policy));
            })
        .RequireRoles(UserRoles.Customer);
    }
}

