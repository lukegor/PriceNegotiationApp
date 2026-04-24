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

namespace PriceNegotiationApp.Application.Negotiations
{
    /// <summary>
    /// <see cref="Negotiation"/> service
    /// </summary>
    public interface INegotiationService
    {
        Task<IEnumerable<NegotiationResultDto>> GetNegotiationsAsync();
        Task<NegotiationResultDto> GetNegotiationAsync(GetNegotiationByIdQuery query, CancellationToken cancellationToken);
        Task<NegotiationResultDto> UpdateNegotiationAsync(UpdateNegotiationCommand command, CancellationToken cancellationToken);
        Task<NegotiationResultDto> ResetRetriesAsync(NegotiationId id, CancellationToken cancellationToken);
        Task<NegotiationResultDto> CloseNegotiationAsync(NegotiationId id, CancellationToken cancellationToken);
        Task<ProposePriceResultDto> ProposeNewPriceAsync(ProposeNewPriceCommand command, CancellationToken cancellationToken);
        Task<NegotiationResultDto> CreateNegotiationAsync(CreateNegotiationCommand command, CancellationToken cancellationToken);
        Task DeleteNegotiationAsync(NegotiationId id, CancellationToken cancellationToken);
    }

    /// <inheritdoc cref="INegotiationService"/>
    public class NegotiationService : INegotiationService
    {
        private readonly INegotiationDomainService _service;
        private readonly NegotiationFactory _negotiationFactory;
        private readonly IAppDbContext _context;
        private readonly IExecutionContext _executionContext;
        private readonly IAuthorizationService _authorizationService;

        public NegotiationService(INegotiationDomainService service, NegotiationFactory negotiationFactory, IAppDbContext context,
            IExecutionContext executionContext, IAuthorizationService authorizationService)
        {
            _service = service;
            _negotiationFactory = negotiationFactory;
            _context = context;
            _executionContext = executionContext;
            _authorizationService = authorizationService;
        }

        public async Task<IEnumerable<NegotiationResultDto>> GetNegotiationsAsync()
        {
            var negotiations = await _context.Negotiations.AsNoTracking().ToListAsync();
            return negotiations.Select(x => x.ToResultDto());
        }

        public async Task<NegotiationResultDto> GetNegotiationAsync(GetNegotiationByIdQuery query, CancellationToken cancellationToken)
        {
            var negotiation = await _context.Negotiations.FindAsync(query, cancellationToken)
                ?? throw new NotFoundException($"Failed to find negotiation with ID '{query.Id}'");

            var authorizationResult = await _authorizationService.AuthorizeAsync(
                _executionContext.User, negotiation, Operations.Read);

            if (!authorizationResult.Succeeded)
            {
                throw new UnauthorizedAccessException("You are not authorized to view this negotiation.");
            }

            return negotiation.ToResultDto();
        }

        [Obsolete("No use case")]
        public async Task<NegotiationResultDto> UpdateNegotiationAsync(UpdateNegotiationCommand command, CancellationToken cancellationToken)
        {
            var existingNegotiation = await _context.Negotiations.FindAsync(command.Id, cancellationToken)
                ?? throw new NotFoundException($"Failed to find negotiation with ID '{command.Id}'");

            //existingNegotiation.Update();

            await _context.SaveChangesAsync(cancellationToken);
            return existingNegotiation.ToResultDto();
        }

        public async Task<NegotiationResultDto> ResetRetriesAsync(NegotiationId id, CancellationToken cancellationToken)
        {
            var negotiation = await _context.Negotiations.FindAsync(id, cancellationToken)
                ?? throw new NotFoundException();

            _service.ResetRetries(negotiation);
            await _context.SaveChangesAsync(cancellationToken);
            return negotiation.ToResultDto();
        }

        public async Task<NegotiationResultDto> CloseNegotiationAsync(NegotiationId id, CancellationToken cancellationToken)
        {
            var existingNegotiation = await _context.Negotiations.FindAsync(id, cancellationToken)
                ?? throw new NotFoundException($"Failed to find negotiation with ID '{id}'");

            var authorizationResult =
                await _authorizationService.AuthorizeAsync(_executionContext.User, existingNegotiation, Operations.Close);

            if (!authorizationResult.Succeeded)
            {
                throw new UnauthorizedAccessException("You are not authorized to close this negotiation.");
            }

            existingNegotiation.Close();
            await _context.SaveChangesAsync(cancellationToken);
            return existingNegotiation.ToResultDto();
        }

        public async Task<ProposePriceResultDto> ProposeNewPriceAsync(ProposeNewPriceCommand command, CancellationToken cancellationToken)
        {
            var negotiation = await _context.Negotiations.FindAsync(command.NegotiationId, cancellationToken)
                ?? throw new NotFoundException($"Failed to find negotiation with ID '{command.NegotiationId}'");

            var authorizationResult =
                await _authorizationService.AuthorizeAsync(_executionContext.User, negotiation, Operations.ProposePrice);

            if (!authorizationResult.Succeeded)
            {
                throw new UnauthorizedAccessException("You are not authorized to propose a new price for this negotiation.");
            }

            _service.TryNegotiate(negotiation, command.ProposedPrice);

            await _context.SaveChangesAsync(cancellationToken);
            return new ProposePriceResultDto { Result = ProposePriceResult.Success };
        }

        public async Task<NegotiationResultDto> CreateNegotiationAsync(CreateNegotiationCommand negotiationDto, CancellationToken cancellationToken)
        {
            var userId = _executionContext.UserId;

            var product = await _context.Products.FindAsync(negotiationDto.ProductId, cancellationToken)
                ?? throw new NotFoundException($"Product with ID '{negotiationDto.ProductId}' was not found.");

            var customer = await _context.Customers.FirstOrDefaultAsync(x => x.IdentityId == userId)
                ?? throw new NotFoundException($"Customer with Identity ID = '{userId}' was not found.");

            Negotiation negotiation = _negotiationFactory.Create(
                negotiationDto.ProductId,
                product.Price.Value,
                negotiationDto.ProposedPrice,
                customer.Id);

            _context.Negotiations.Add(negotiation);
            await _context.SaveChangesAsync(cancellationToken);

            return negotiation.ToResultDto();
        }

        public async Task DeleteNegotiationAsync(NegotiationId id, CancellationToken cancellationToken)
        {
            var negotiation = await _context.Negotiations.FindAsync(id, cancellationToken)
                ?? throw new NotFoundException($"Negotiation with ID '{id}' was not found.");

            _context.Negotiations.Remove(negotiation);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
