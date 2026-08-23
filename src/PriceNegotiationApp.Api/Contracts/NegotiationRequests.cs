using System.ComponentModel.DataAnnotations;

namespace PriceNegotiationApp.Api.Contracts;

public sealed class CreateNegotiationRequest
{
    [Required]
    public Guid ProductId { get; init; }

    [Required]
    [Range(0.01, 999_999_999)]
    public decimal ProposedPrice { get; init; }
}

public sealed class CounterProposalRequest
{
    [Required]
    [Range(0.01, 999_999_999)]
    public decimal ProposedPrice { get; init; }
}
