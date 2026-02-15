using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Common.Exceptions;
using PriceNegotiationApp.Application.Negotiations.Dtos;
using PriceNegotiationApp.Application.Negotiations.Mappers;
using PriceNegotiationApp.Application.Negotiations.Requests.Commands;
using PriceNegotiationApp.Application.Negotiations.Requests.Queries;
using PriceNegotiationApp.Application.Security;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Application.Negotiations
{
    /// <summary>
    /// <see cref="Negotiation"/> service
    /// </summary>
    public interface INegotiationService
    {
        Task<IEnumerable<NegotiationResultDto>> GetNegotiationsAsync();
        Task<NegotiationResultDto> GetNegotiationAsync(GetNegotiationByIdQuery query);
        Task<NegotiationResultDto> UpdateNegotiationAsync(UpdateNegotiationCommand command);
        Task<NegotiationResultDto> CloseNegotiationAsync(NegotiationId id);
        Task<ProposePriceResultDto> ProposeNewPriceAsync(ProposeNewPriceCommand command);
        Task<NegotiationResultDto> CreateNegotiationAsync(CreateNegotiationCommand command);
        Task DeleteNegotiationAsync(NegotiationId id);
    }

    /// <inheritdoc cref="INegotiationService"/>
    public class NegotiationService : INegotiationService
    {
        private readonly IAppDbContext _context;
        private readonly IExecutionContext _executionContext;
        private readonly NegotiationFactory _negotiationFactory;
        private readonly IAuthorizationService _authorizationService;

        public NegotiationService(IAppDbContext context, IExecutionContext executionContext, NegotiationFactory negotiationFactory,
            IAuthorizationService authorizationService)
        {
            _context = context;
            _executionContext = executionContext;
            _negotiationFactory = negotiationFactory;
            _authorizationService = authorizationService;
        }

        public async Task<IEnumerable<NegotiationResultDto>> GetNegotiationsAsync()
        {
            var negotiations = await _context.Negotiations.AsNoTracking().ToListAsync();
            return negotiations.Select(x => x.ToResultDto());
        }

        public async Task<NegotiationResultDto> GetNegotiationAsync(GetNegotiationByIdQuery query)
        {
            var negotiation = await _context.Negotiations.FindAsync(query)
                ?? throw new NotFoundException($"Failed to find negotiation with ID '{query.Id}'");

            await _authorizationService.AuthorizeAsync(_executionContext.User, negotiation, PolicyNames.Read);

            return negotiation.ToResultDto();
        }

        [Obsolete("No use case")]
        public async Task<NegotiationResultDto> UpdateNegotiationAsync(UpdateNegotiationCommand command)
        {
            var existingNegotiation = await _context.Negotiations.FindAsync(command.Id)
                ?? throw new NotFoundException($"Update failed: Negotiation with ID {command.Id} was not found.");

            //existingNegotiation.Update();

            await _context.SaveChangesAsync();
            return existingNegotiation.ToResultDto();
        }

        public async Task<NegotiationResultDto> CloseNegotiationAsync(NegotiationId id)
        {
            var existingNegotiation = await _context.Negotiations.FindAsync(id)
                ?? throw new NotFoundException($"Update failed: Negotiation with ID {id} was not found.");

            await _authorizationService.AuthorizeAsync(_executionContext.User, existingNegotiation, PolicyNames.ModifyNegotiationAsOwner);

            existingNegotiation.Close();
            await _context.SaveChangesAsync();
            return existingNegotiation.ToResultDto();
        }

        public async Task<ProposePriceResultDto> ProposeNewPriceAsync(ProposeNewPriceCommand command)
        {
            var negotiation = await _context.Negotiations.FindAsync(command.NegotiationId) ??
                throw new NotFoundException("Negotiation not found");

            await _authorizationService.AuthorizeAsync(_executionContext.User, negotiation, PolicyNames.ModifyNegotiationAsOwner);

            Product relevantProduct = await _context.Products.FindAsync(negotiation.ProductId)
                ?? throw new NotFoundException($"Product associated with negotiation '{command.NegotiationId}' not found");

            negotiation.TryNegotiate(command.ProposedPrice, relevantProduct.Price.Value);

            await _context.SaveChangesAsync();
            return new ProposePriceResultDto { Result = ProposePriceResult.Success };
        }

        public async Task<NegotiationResultDto> CreateNegotiationAsync(CreateNegotiationCommand negotiationDto)
        {
            var userId = _executionContext.UserId;

            var product = await _context.Products.FindAsync(negotiationDto.ProductId)
                ?? throw new NotFoundException($"Product with ID '{negotiationDto.ProductId}' was not found.");

            var customer = await _context.Customers.FirstOrDefaultAsync(x => x.IdentityId == userId)
                ?? throw new NotFoundException($"Customer with Identity ID = '{userId}' was not found.");

            Negotiation negotiation = _negotiationFactory.Create(
                negotiationDto.ProductId,
                product.Price.Value,
                negotiationDto.ProposedPrice,
                customer.Id);

            _context.Negotiations.Add(negotiation);
            await _context.SaveChangesAsync();

            return negotiation.ToResultDto();
        }

        public async Task DeleteNegotiationAsync(NegotiationId id)
        {
            var negotiation = await _context.Negotiations.FindAsync(id)
                ?? throw new NotFoundException($"Negotiation with ID '{id}' was not found.");

            _context.Negotiations.Remove(negotiation);
            await _context.SaveChangesAsync();
        }
    }
}
