using System.Globalization;
using PriceNegotiationApp.BuildingBlocks;

namespace PriceNegotiationApp.Modules.Negotiations.Domain;

public sealed class ProposalExceedsLimitException(decimal limit)
    : DomainException($"Proposal exceeds the allowed limit of {limit.ToString(CultureInfo.InvariantCulture)}.")
{
    public decimal Limit { get; } = limit;
}
