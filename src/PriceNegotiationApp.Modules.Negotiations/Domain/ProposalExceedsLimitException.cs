using PriceNegotiationApp.SharedKernel;
using System.Globalization;

namespace PriceNegotiationApp.Modules.Negotiations.Domain;

internal sealed class ProposalExceedsLimitException(decimal limit)
    : DomainException($"Proposal exceeds the allowed limit of {limit.ToString(CultureInfo.InvariantCulture)}.")
{
    public decimal Limit { get; } = limit;
}
