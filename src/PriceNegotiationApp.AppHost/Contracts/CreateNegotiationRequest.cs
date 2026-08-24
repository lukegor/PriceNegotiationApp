namespace PriceNegotiationApp.AppHost.Contracts;

public sealed class CreateNegotiationRequest
{
    public Guid ProductId { get; init; }

    public decimal ProposedPrice { get; init; }
}

