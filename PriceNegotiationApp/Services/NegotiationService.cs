using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Models.DTO;
using PriceNegotiationApp.Models.Input_Models;
using PriceNegotiationApp.Services.Providers;
using PriceNegotiationApp.Utility;
using PriceNegotiationApp.Utility.Custom_Exceptions;
using System.Runtime.CompilerServices;
using System.Security.Claims;

namespace PriceNegotiationApp.Services
{
	public interface INegotiationService
	{
		Task<IEnumerable<Negotiation>> GetNegotiationsAsync();
		Task<Negotiation> GetNegotiationAsync(int id);
		Task<UpdateResultType> UpdateNegotiationAsync(int id, Negotiation Negotiation);
		Task<ProposePriceResponse> ProposeNewPriceAsync(int negotiationId, decimal proposedPrice);
		Task<UpdateResultType> RespondToNegotiationProposalAsync(int negotiationId, bool isApproved);
        Task<Negotiation> CreateNegotiationAsync(NegotiationInputModel Negotiation);
		Task<bool> DeleteNegotiationAsync(int id);
		bool NegotiationExists(int id);
		bool IsUserAssociatedWithNegotiation(int negotiationId);
		string GetLoggedInUserRole();

    }

	public class NegotiationService: INegotiationService
	{
		//private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly AppDbContext _context;
        private readonly IClaimsProvider _claimsProvider;
		private readonly ILogger<NegotiationService> _logger;


		public NegotiationService(AppDbContext context, IClaimsProvider claimsProvider, ILogger<NegotiationService> logger)
		{
			_context = context;
			_claimsProvider = claimsProvider;
			_logger = logger;
		}

		public async Task<IEnumerable<Negotiation>> GetNegotiationsAsync()
		{
			var negotiations = await _context.Negotiations.ToListAsync();
			_logger.LogInformation("List of {Count} negotiations was returned.", negotiations.Count);
			return negotiations;
		}

		public async Task<Negotiation> GetNegotiationAsync(int id)
		{
			var negotiation = await _context.Negotiations.FindAsync(id);

			if (negotiation == null)
			{
				throw new NotFoundException();
			}
			_logger.LogInformation("Negotiation with ID '{Id}' was found.", negotiation.Id);

			return negotiation;
		}

		public async Task<UpdateResultType> UpdateNegotiationAsync(int id, Negotiation Negotiation)
		{
			try
			{
				var existingNegotiation = await GetNegotiationAsync(id);
				var idInDb = existingNegotiation.Id;
				if (id != idInDb)
				{
					_logger.LogWarning("Update failed: Provided ID {ProvidedId} does not match the ID {IdInDb} in the database.", id, idInDb);
					return UpdateResultType.NotFound;
				}
			}
			catch (NotFoundException)
			{
				_logger.LogWarning("Update failed: Negotiation with ID {Id} was not found.", id);
				return UpdateResultType.NotFound;
			}

			_context.Entry(Negotiation).State = EntityState.Modified;

			try
			{
				await _context.SaveChangesAsync();
				_logger.LogInformation("Negotiation with ID {Id} updated successfully.", id);
			}
			catch (DbUpdateConcurrencyException)
			{
				_logger.LogError("Concurrency exception occurred while updating negotiation with ID '{Id}'", id);
				return UpdateResultType.Conflict;
			}

			return UpdateResultType.Success;
		}

		public async Task<ProposePriceResponse> ProposeNewPriceAsync(int negotiationId, decimal proposedPrice)
		{
			var negotiation = await _context.Negotiations.FindAsync(negotiationId);

			if (negotiation == null)
			{
				_logger.LogWarning("Negotiation with ID '{Id}' not found.", negotiationId);
				return new ProposePriceResponse { Result = ProposePriceResult.NotFound };
			}

			//var isUserAssociated = IsUserAssociatedWithNegotiation(negotiationId);

			//if (!isUserAssociated)
			//{
			//	return new ProposePriceResponse { Result = ProposePriceResult.Unauthorized };
			//}

			Product relevantProduct = await FindRelevantProductAsync(negotiation);

			const int Multiplier = 2;

			if (negotiation.RetriesLeft <= 0)
			{
				_logger.LogWarning("ProposeNewPrice failed: Incorrect action for negotiation with ID '{Id}'.", negotiation.Id);
				return new ProposePriceResponse { Result = ProposePriceResult.IncorrectAction };
			}

			decimal maxAllowedPriceProposition = CalculateMaxAllowedPrice(Multiplier, relevantProduct.Price);
			if (proposedPrice <= 0 || proposedPrice > maxAllowedPriceProposition)
			{
				_logger.LogWarning("ProposeNewPrice failed: Invalid input for negotiation with ID '{Id}'.", negotiation.Id);
				return new ProposePriceResponse
				{
					Result = ProposePriceResult.InvalidInput,
					MaxAllowedPrice = maxAllowedPriceProposition
				};
			}

			// update negotiation fields values
			negotiation.ProposeNewPrice(proposedPrice);

			try
			{
				await _context.SaveChangesAsync();
				_logger.LogInformation("ProposeNewPrice succeeded for negotiation with ID '{Id}'.", negotiation.Id);
				return new ProposePriceResponse { Result = ProposePriceResult.Success };
			}
			catch (DbUpdateException)
			{
				_logger.LogError("ProposeNewPrice failed: Error occurred while updating negotiation with ID '{Id}'.", negotiation.Id);
				return new ProposePriceResponse { Result = ProposePriceResult.Error };
			}
		}

		public async Task<UpdateResultType> RespondToNegotiationProposalAsync(int negotiationId, bool isApproved)
		{
			var negotiation = await _context.Negotiations.FindAsync(negotiationId);

            if (negotiation == null)
            {
				_logger.LogWarning("Negotiation with ID '{Id}' not found.", negotiationId);
				return UpdateResultType.NotFound;
            }

            if (isApproved)
			{
				negotiation.IsAccepted = true;
				negotiation.Status = NegotiationStatus.Closed;
			}
			else
			{
				if (negotiation.RetriesLeft <= 0)
				{
					negotiation.IsAccepted = false;
					negotiation.Status = NegotiationStatus.Closed;
				}
			}

			negotiation.UpdatedAt = DateTime.Now;

			return await UpdateNegotiationAsync(negotiation.Id, negotiation);
		}

		public async Task<Negotiation> CreateNegotiationAsync(NegotiationInputModel negotiationDetails)
		{
			string userId = _claimsProvider.UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);//_httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

			Negotiation negotiation = new Negotiation(negotiationDetails.ProductId, negotiationDetails.ProposedPrice, userId);

			_context.Negotiations.Add(negotiation);
			await _context.SaveChangesAsync();

			_logger.LogInformation("Negotiation with ID '{Id}' created successfully.", negotiation.Id);

			return negotiation;
		}

		public async Task<bool> DeleteNegotiationAsync(int id)
		{
			var negotiation = await _context.Negotiations.FindAsync(id);
			if (negotiation == null)
			{
				_logger.LogWarning("Negotiation with ID '{Id}' was not found.", id);
				return false;
			}

			_context.Negotiations.Remove(negotiation);
			await _context.SaveChangesAsync();

			_logger.LogInformation("Negotiation with ID {Id} was deleted successfully.", id);


			return true;
		}

		public bool NegotiationExists(int id)
		{
			bool exists = _context.Negotiations.Any(e => e.Id == id);

			_logger.LogInformation(exists ? $"Negotiation with ID '{id}' exists." : $"Negotiation with ID '{id}' does not exist.");

			return exists;
		}

		public string GetLoggedInUserRole()
		{
			var userRole = _claimsProvider.UserClaimsPrincipal.FindFirstValue(ClaimTypes.Role);//_httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value;
            System.Diagnostics.Debug.WriteLine(userRole);
			return userRole;
		}

		public bool IsUserAssociatedWithNegotiation(int negotiationId)
		{
			var negotiation = GetNegotiationAsync(negotiationId).Result;

			if (negotiation == null)
			{
				return false;
			}

			var userId = negotiation.UserId; // Retrieve userId associated with certain negotiation
			var loggedInUserId = _claimsProvider.UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);//_httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return userId == loggedInUserId;
		}

		public async Task<Product> FindRelevantProductAsync(Negotiation negotiation)
		{
			var dbNegotiation = await _context.Negotiations.FirstOrDefaultAsync(e => e.Id == negotiation.Id);
			var productId = dbNegotiation.ProductId;
			Product product = await _context.Products.FindAsync(productId);

			return product;
		}

		private decimal CalculateMaxAllowedPrice(int multiplier, decimal productPrice)
		{
			return multiplier * productPrice;
		}
	}
}
