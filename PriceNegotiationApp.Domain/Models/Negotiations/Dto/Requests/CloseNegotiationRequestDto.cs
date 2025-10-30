using PriceNegotiationApp.Domain.Models.Negotiations;

namespace PriceNegotiationApp.Domain.Models.Negotiations.Dto.Requests
{
    public class CloseNegotiationRequestDto
    {
        public string Id { get; init; }
        public NegotiationStatus Status { get; private init; } = NegotiationStatus.Closed;

        public CloseNegotiationRequestDto(string id)
        {
            Id = id;
        }
    }
}
