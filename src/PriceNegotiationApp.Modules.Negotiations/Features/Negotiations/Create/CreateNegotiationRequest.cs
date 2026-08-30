namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.Create;

internal sealed class CreateNegotiationRequest
{
    public Guid ProductId { get; init; }

    public decimal ProposedPrice { get; init; }
}
