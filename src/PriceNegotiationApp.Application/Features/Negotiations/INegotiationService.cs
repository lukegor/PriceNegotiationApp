using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Responses;

namespace PriceNegotiationApp.Application.Features.Negotiations;

public interface INegotiationService
{
    Task<NegotiationResponse> CreateAsync(CallerContext caller, Guid productId, decimal proposedPrice, CancellationToken ct);

    Task<NegotiationResponse> GetAsync(CallerContext caller, Guid id, CancellationToken ct);

    Task<PagedResult<NegotiationResponse>> ListMineAsync(CallerContext caller, PageQuery page, CancellationToken ct);

    Task<PagedResult<NegotiationResponse>> ListAsync(PageQuery page, CancellationToken ct);

    Task<CounterProposalOutcome> CounterProposeAsync(CallerContext caller, Guid id, decimal proposedPrice, CancellationToken ct);

    Task<NegotiationResponse> AcceptAsync(Guid id, CancellationToken ct);

    Task<NegotiationResponse> DeclineAsync(Guid id, CancellationToken ct);

    Task WithdrawAsync(CallerContext caller, Guid id, CancellationToken ct);
}
