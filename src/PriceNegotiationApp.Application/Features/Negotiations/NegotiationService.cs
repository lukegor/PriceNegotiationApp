using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Exceptions;
using PriceNegotiationApp.Application.Responses;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.Policy;
using PriceNegotiationApp.Domain.ValueObjects;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Application.Features.Negotiations;

public sealed class NegotiationService(
    INegotiationRepository negotiations,
    IProductRepository products,
    ICustomerRepository customers,
    INegotiationPolicy policy,
    IUnitOfWork uow,
    TimeProvider time) : INegotiationService
{
    public async Task<NegotiationResponse> CreateAsync(CallerContext caller, Guid productId, decimal proposedPrice, CancellationToken ct)
    {
        var product = await products.GetAsync(ProductId.From(productId), ct)
                      ?? throw new NotFoundException(nameof(Product), productId);

        if (await negotiations.FindOpenAsync(product.Id, caller.UserId, ct) is not null)
        {
            throw new ConflictException(ErrorCodes.NegotiationAlreadyOpen, "An open negotiation already exists for this product.");
        }

        var customerId = await customers.GetOrCreateAsync(caller.UserId, ct);
        var negotiation = Negotiation.Start(customerId, product, proposedPrice, time.GetUtcNow(), policy);
        await negotiations.AddAsync(negotiation, ct);
        await uow.SaveChangesAsync(ct);
        return Map(negotiation);
    }

    public async Task<NegotiationResponse> GetAsync(CallerContext caller, Guid id, CancellationToken ct)
    {
        var negotiation = await RequireAccessibleAsync(caller, id, ct);
        return Map(negotiation);
    }

    public async Task<PagedResult<NegotiationResponse>> ListMineAsync(CallerContext caller, PageQuery page, CancellationToken ct)
    {
        var customer = await customers.GetByIdentityAsync(caller.UserId, ct);
        var q = negotiations.Query().Where(n => customer != null && n.CustomerId == customer.Id);
        return await ToPagedAsync(q, page, ct);
    }

    public async Task<PagedResult<NegotiationResponse>> ListAsync(PageQuery page, CancellationToken ct) =>
        await ToPagedAsync(negotiations.Query(), page, ct);

    public async Task<CounterProposalOutcome> CounterProposeAsync(CallerContext caller, Guid id, decimal proposedPrice, CancellationToken ct)
    {
        var negotiation = await RequireOwnerAsync(caller, id, ct);

        var outcome = negotiation.CounterPropose(proposedPrice, time.GetUtcNow(), policy);
        if (outcome == NegotiationOutcome.NoProposalsRemaining)
        {
            throw new ConflictException(ErrorCodes.NoProposalsRemaining, "No proposals remain for this negotiation.");
        }

        await uow.SaveChangesAsync(ct);
        return new CounterProposalOutcome(outcome.ToString(), Map(negotiation));
    }

    public async Task<NegotiationResponse> AcceptAsync(Guid id, CancellationToken ct)
    {
        var negotiation = await RequireAsync(id, ct);
        negotiation.Accept(time.GetUtcNow());
        await uow.SaveChangesAsync(ct);
        return Map(negotiation);
    }

    public async Task<NegotiationResponse> DeclineAsync(Guid id, CancellationToken ct)
    {
        var negotiation = await RequireAsync(id, ct);
        negotiation.Decline(time.GetUtcNow());
        await uow.SaveChangesAsync(ct);
        return Map(negotiation);
    }

    public async Task WithdrawAsync(CallerContext caller, Guid id, CancellationToken ct)
    {
        var negotiation = await RequireAsync(id, ct);
        if (!caller.IsInRole(UserRoles.Admin) && !await IsOwnerAsync(caller, negotiation, ct))
        {
            throw new ForbiddenAccessException();
        }

        negotiations.Remove(negotiation);
        await uow.SaveChangesAsync(ct);
    }

    private async Task<Negotiation> RequireAsync(Guid id, CancellationToken ct) =>
        await negotiations.GetAsync(NegotiationId.From(id), ct)
        ?? throw new NotFoundException(nameof(Negotiation), id);

    private async Task<Negotiation> RequireOwnerAsync(CallerContext caller, Guid id, CancellationToken ct)
    {
        var negotiation = await RequireAsync(id, ct);
        if (!await IsOwnerAsync(caller, negotiation, ct))
        {
            throw new ForbiddenAccessException();
        }

        return negotiation;
    }

    private async Task<Negotiation> RequireAccessibleAsync(CallerContext caller, Guid id, CancellationToken ct)
    {
        var negotiation = await RequireAsync(id, ct);
        if (caller.IsInRole(UserRoles.Admin) || caller.IsInRole(UserRoles.Staff) || await IsOwnerAsync(caller, negotiation, ct))
        {
            return negotiation;
        }

        throw new ForbiddenAccessException();
    }

    private async Task<bool> IsOwnerAsync(CallerContext caller, Negotiation negotiation, CancellationToken ct)
    {
        var customer = await customers.GetByIdentityAsync(caller.UserId, ct);
        return customer is not null && customer.Id == negotiation.CustomerId;
    }

    private async Task<PagedResult<NegotiationResponse>> ToPagedAsync(
        IQueryable<Negotiation> q, PageQuery page, CancellationToken ct)
    {
        var total = await q.LongCountAsync(ct);
        var items = await q
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip(page.Skip).Take(page.SafePageSize)
            .ToListAsync(ct);
        return new PagedResult<NegotiationResponse>(
            items.Select(Map).ToList(), page.SafePage, page.SafePageSize, total);
    }

    private NegotiationResponse Map(Negotiation n) => new(
        n.Id.Value, n.ProductId.Value, n.BasePrice, n.CurrentOffer,
        n.Status.ToString(), n.ProposalsUsed, n.RemainingProposals(policy),
        n.CreatedAtUtc, n.LastProposalAtUtc, n.DecidedAtUtc);
}



