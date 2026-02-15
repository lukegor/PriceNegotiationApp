using PriceNegotiationApp.Application.Negotiations.Requests.Commands;
using PriceNegotiationApp.Contracts.Negotiations.Dto.Requests;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;

namespace PriceNegotiationApp.Presentation.Negotiations.Mappers
{
    public static class UpdateNegotiationMapperExtensions
    {
        extension(UpdateNegotiationRequestDto command)
        {
            public UpdateNegotiationCommand ToCommand(NegotiationId negotiationId)
            {
                return new UpdateNegotiationCommand(
                    negotiationId,
                    new ProposedPrice(command.ProposedPrice));
            }
        }
    }
}
