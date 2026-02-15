using PriceNegotiationApp.Application.Negotiations.Requests.Commands;
using PriceNegotiationApp.Contracts.Negotiations.Dto.Requests;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;
using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Presentation.Negotiations.Mappers
{
    public static class CreateNegotiationMapperExtensions
    {
        extension(CreateNegotiationRequestDto command)
        {
            public CreateNegotiationCommand ToCommand()
            {
                return new CreateNegotiationCommand(
                    ProductId.From(command.ProductId),
                    new ProposedPrice(command.ProposedPrice));
            }
        }
    }
}
