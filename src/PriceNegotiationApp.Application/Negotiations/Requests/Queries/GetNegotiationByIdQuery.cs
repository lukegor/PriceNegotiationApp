using PriceNegotiationApp.Domain.Models.Negotiations;

namespace PriceNegotiationApp.Application.Negotiations.Requests.Queries
{
    public record GetNegotiationByIdQuery(
        NegotiationId Id);
}
