using PriceNegotiationApp.Domain.Models.Negotiations.Enums;

namespace PriceNegotiationApp.Domain.Models.Negotiations
{
    public record NegotiationResult(
        NegotiationOutcome Status,
        decimal MaxAllowedPrice)
    {
        public bool IsSuccess => Status == NegotiationOutcome.Success;

        public static NegotiationResult Success(decimal max)
            => new(NegotiationOutcome.Success, max);

        public static NegotiationResult Failure(decimal max)
            => new(NegotiationOutcome.Failed, max);
    }
}
