using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Data;
using PriceNegotiationApp.Domain.Models.Dto;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Negotiations.Dto.Requests;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Services.Providers;
using PriceNegotiationApp.Utility.Utility;
using PriceNegotiationApp.Utility.Utility.Exceptions;
using System.Runtime.CompilerServices;
using System.Security.Claims;

namespace PriceNegotiationApp.Services
{
    /// <summary>
    /// <see cref="Negotiation"/> domain service
    /// </summary>
    public interface INegotiationService
	{
		Task<IEnumerable<Negotiation>> GetNegotiationsAsync();
		Task<Negotiation> GetNegotiationAsync(string id);
        Task<Negotiation> UpdateNegotiationAsync(string id, UpdateNegotiationRequestDto Negotiation);
		Task<Negotiation> CloseNegotiationAsync(string id);
		Task<ProposePriceResponseDto> ProposeNewPriceAsync(string negotiationId, decimal proposedPrice);
        Task<Negotiation> CreateNegotiationAsync(CreateNegotiationRequestDto Negotiation);
		Task DeleteNegotiationAsync(string id);
		Task<bool> IsUserAssociatedWithNegotiation(string negotiationId);
    }

    /// <inheritdoc cref="INegotiationService"/>
    public class NegotiationService: INegotiationService
	{
		private readonly AppDbContext _context;
        private readonly IExecutionContext _executionContext;
		private readonly ILogger<NegotiationService> _logger;

		public NegotiationService(AppDbContext context, IExecutionContext executionContext, ILogger<NegotiationService> logger)
		{
			_context = context;
			_executionContext = executionContext;
			_logger = logger;
		}

		public async Task<IEnumerable<Negotiation>> GetNegotiationsAsync()
		{
			var negotiations = await _context.Negotiations.ToListAsync();
			return negotiations;
		}

		public async Task<Negotiation> GetNegotiationAsync(string id)
		{
			var negotiation = await _context.Negotiations.FindAsync(id);

			if (negotiation == null)
			{
				throw new NotFoundException($"Failed to find negotiation with ID '{id}'");
			}

			return negotiation;
		}

		[Obsolete("No use case")]
		public async Task<Negotiation> UpdateNegotiationAsync(string id, UpdateNegotiationRequestDto negotiation)
		{
            var existingNegotiation = await _context.Negotiations.FindAsync(id);

            if (existingNegotiation == null)
			{
				throw new NotFoundException($"Update failed: Negotiation with ID {id} was not found.");
			}

			//existingNegotiation.Update();

			await _context.SaveChangesAsync();
			return existingNegotiation;
        }

        public async Task<Negotiation> CloseNegotiationAsync(string id)
		{
            var existingNegotiation = await _context.Negotiations.FindAsync(id);

            if (existingNegotiation == null)
            {
                throw new NotFoundException($"Update failed: Negotiation with ID {id} was not found.");
            }

			existingNegotiation.Close();
			await _context.SaveChangesAsync();
			return existingNegotiation;
        }

        public async Task<ProposePriceResponseDto> ProposeNewPriceAsync(string negotiationId, decimal proposedPrice)
		{
			var negotiation = await _context.Negotiations.FindAsync(negotiationId);

			if (negotiation == null)
			{
				throw new NotFoundException("Negotiation not found");
			}

			//var isUserAssociated = IsUserAssociatedWithNegotiation(negotiationId);

			//if (!isUserAssociated)
			//{
			//	return new ProposePriceResponse { Result = ProposePriceResult.Unauthorized };
			//}

			Product relevantProduct = await _context.Products.FindAsync(negotiation.ProductId);

			if (relevantProduct == null)
			{
				throw new NotFoundException("Associated product not found");
            }

            negotiation.TryNegotiate(proposedPrice, relevantProduct.Price.Value);

			await _context.SaveChangesAsync();
			return new ProposePriceResponseDto { Result = ProposePriceResult.Success };
		}

		public async Task<Negotiation> CreateNegotiationAsync(CreateNegotiationRequestDto negotiationDetails)
		{
			string userId = _executionContext.UserId;

            var product = await _context.Products.FindAsync(negotiationDetails.ProductId);

            if (product == null)
            {
				throw new NotFoundException($"Product with ID '{negotiationDetails.ProductId}' was not found.");
            }

            Negotiation negotiation = new Negotiation(
				negotiationDetails.ProductId,
				product.Price.Value,
				new ProposedPrice(negotiationDetails.ProposedPrice),
                userId);

			_context.Negotiations.Add(negotiation);
			await _context.SaveChangesAsync();

			return negotiation;
		}

		public async Task DeleteNegotiationAsync(string id)
		{
			var negotiation = await _context.Negotiations.FindAsync(id);
			if (negotiation == null)
			{
				throw new NotFoundException($"Negotiation with ID '{id}' was not found.");
			}

			_context.Negotiations.Remove(negotiation);
			await _context.SaveChangesAsync();
		}

		private bool NegotiationExists(string id)
		{
			bool exists = _context.Negotiations.Any(e => e.Id == id);

			_logger.LogInformation(exists ? $"Negotiation with ID '{id}' exists." : $"Negotiation with ID '{id}' does not exist.");

			return exists;
		}

		public async Task<bool> IsUserAssociatedWithNegotiation(string negotiationId)
		{
			var negotiation = await _context.Negotiations.FindAsync(negotiationId);

            if (negotiation == null)
			{
				return false;
			}

			var userId = negotiation.UserId; // Retrieve userId associated with certain negotiation
			var loggedInUserId = _executionContext.UserId;

            return userId == loggedInUserId;
		}
	}
}
