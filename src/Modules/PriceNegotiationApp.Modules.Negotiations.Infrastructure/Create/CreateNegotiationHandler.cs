using PriceNegotiationApp.Modules.Negotiations.Contracts;
using PriceNegotiationApp.Modules.Negotiations.Infrastructure;
using PriceNegotiationApp.Modules.Negotiations.Application.Create;
using PriceNegotiationApp.Modules.Catalog.Contracts;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Application;
using PriceNegotiationApp.Modules.Negotiations.Infrastructure.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Infrastructure.Create;

internal sealed class CreateNegotiationHandler(
    NegotiationsDbContext db,
    IProductPriceProvider products,
    INegotiationPolicy policy,
    TimeProvider clock)
{
    public async Task<NegotiationResponse> HandleAsync(
        CreateNegotiationRequest command, CallerContext caller, CancellationToken ct)
    {
        var snapshot = await products.GetAsync(command.ProductId, ct)
                       ?? throw new NotFoundException("Product", command.ProductId);

        if (await NegotiationAccess.FindOpenAsync(db, snapshot.ProductId, caller.UserId, ct) is not null)
        {
            throw new ConflictException(NegotiationErrorCodes.NegotiationAlreadyOpen,
                "An open negotiation already exists for this product.");
        }

        // Provisioning the customer row and inserting the negotiation commit together:
        // a failed insert must not strand a permanent customer row (one commit point).
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var customerId = await NegotiationAccess.GetOrCreateCustomerIdAsync(db, caller.UserId, ct);
        var negotiation = Negotiation.Start(customerId, snapshot.ProductId, snapshot.Price,
            command.ProposedPrice, clock.GetUtcNow(), policy);
        await db.Negotiations.AddAsync(negotiation, ct);

        // The partial unique index is the real guard; a race that slipped past the
        // pre-check above surfaces here as a 409 instead of a 500.
        await db.SaveOrConflictAsync(
            _ => new ConflictException(NegotiationErrorCodes.NegotiationAlreadyOpen,
                "An open negotiation already exists for this product."), ct);
        await tx.CommitAsync(ct);

        return NegotiationResponses.ToResponse(negotiation);
    }
}
