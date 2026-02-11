using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Common.Exceptions;
using PriceNegotiationApp.Application.Common.Identities.Dtos.Responses;
using PriceNegotiationApp.Application.Negotiations.Dto.Requests.CreateNegotiation;
using PriceNegotiationApp.Application.Negotiations.Dto.Requests.UpdateNegotiation;
using PriceNegotiationApp.Application.Negotiations.Dto.Response;
using PriceNegotiationApp.Application.Negotiations.Mappers;
using PriceNegotiationApp.Application.Security;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;
using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Application.Services
{
    /// <summary>
    /// <see cref="Negotiation"/> domain service
    /// </summary>
    public interface INegotiationService
    {
        Task<IEnumerable<Negotiation>> GetNegotiationsAsync();
        Task<Negotiation> GetNegotiationAsync(NegotiationId id);
        Task<Negotiation> UpdateNegotiationAsync(NegotiationId id, UpdateNegotiationRequestDto request);
        Task<NegotiationResponseDto> CloseNegotiationAsync(NegotiationId id);
        Task<ProposePriceResponseDto> ProposeNewPriceAsync(NegotiationId negotiationId, decimal proposedPrice);
        Task<NegotiationResponseDto> CreateNegotiationAsync(CreateNegotiationRequestDto negotiationDto);
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

        public async Task<IEnumerable<Negotiation>> GetNegotiationsAsync()
        {
            var negotiations = await _context.Negotiations.ToListAsync();
            return negotiations;
        }

        public async Task<Negotiation> GetNegotiationAsync(NegotiationId id)
        {
            var negotiation = await _context.Negotiations.FindAsync(id)
                ?? throw new NotFoundException($"Failed to find negotiation with ID '{id}'");

            await _authorizationService.AuthorizeAsync(_executionContext.User, negotiation, PolicyNames.Read);

            return negotiation;
        }

        [Obsolete("No use case")]
        public async Task<Negotiation> UpdateNegotiationAsync(NegotiationId id, UpdateNegotiationRequestDto request)
        {
            var existingNegotiation = await _context.Negotiations.FindAsync(id)
                ?? throw new NotFoundException($"Update failed: Negotiation with ID {id} was not found.");

            //existingNegotiation.Update();

            await _context.SaveChangesAsync();
            return existingNegotiation;
        }

        public async Task<NegotiationResponseDto> CloseNegotiationAsync(NegotiationId id)
        {
            var existingNegotiation = await _context.Negotiations.FindAsync(id)
                ?? throw new NotFoundException($"Update failed: Negotiation with ID {id} was not found.");

            await _authorizationService.AuthorizeAsync(_executionContext.User, existingNegotiation, PolicyNames.ModifyNegotiationAsOwner);

            existingNegotiation.Close();
            await _context.SaveChangesAsync();
            return existingNegotiation.ToResponseDto();
        }

        public async Task<ProposePriceResponseDto> ProposeNewPriceAsync(NegotiationId negotiationId, decimal proposedPrice)
        {
            var negotiation = await _context.Negotiations.FindAsync(negotiationId) ??
                throw new NotFoundException("Negotiation not found");

            await _authorizationService.AuthorizeAsync(_executionContext.User, negotiation, PolicyNames.ModifyNegotiationAsOwner);

            Product relevantProduct = await _context.Products.FindAsync(negotiation.ProductId)
                ?? throw new NotFoundException($"Product associated with negotiation '{negotiationId}' not found");

            negotiation.TryNegotiate(proposedPrice, relevantProduct.Price.Value);

            await _context.SaveChangesAsync();
            return new ProposePriceResponseDto { Result = ProposePriceResultResponseDto.Success };
        }

        public async Task<NegotiationResponseDto> CreateNegotiationAsync(CreateNegotiationRequestDto negotiationDto)
        {
            var userId = _executionContext.UserId;

            var product = await _context.Products.FindAsync(negotiationDto.ProductId)
                ?? throw new NotFoundException($"Product with ID '{negotiationDto.ProductId}' was not found.");

            Negotiation negotiation = _negotiationFactory.Create(
                negotiationDto.ProductId,
                product.Price.Value,
                new ProposedPrice(negotiationDto.ProposedPrice),
                userId!.Value);

            _context.Negotiations.Add(negotiation);
            await _context.SaveChangesAsync();

            return negotiation.ToResponseDto();
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
