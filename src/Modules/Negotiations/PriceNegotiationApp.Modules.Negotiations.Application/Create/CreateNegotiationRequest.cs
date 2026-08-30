namespace PriceNegotiationApp.Modules.Negotiations.Application.Create;

internal sealed class CreateNegotiationRequest
{
    public Guid ProductId { get; init; }

    public decimal ProposedPrice { get; init; }
}
