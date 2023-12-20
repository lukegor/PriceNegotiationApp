using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Models.DTO;
using PriceNegotiationApp.Models.Input_Models;
using PriceNegotiationApp.Utility;
using System.Runtime.CompilerServices;
using System.Security.Claims;

namespace PriceNegotiationApp.Services
{
	public interface INegotiationService
	{
		Task<IEnumerable<Negotiation>> GetNegotiationsAsync();
		Task<Negotiation> GetNegotiationAsync(int id);
		Task<UpdateResultType> UpdateNegotiationAsync(int id, Negotiation Negotiation);
		Task<Negotiation> CreateNegotiationAsync(NegotiationInputModel Negotiation);
		Task<bool> DeleteNegotiationAsync(int id);
	}

	public class NegotiationService: INegotiationService
	{
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly AppDbContext _context;

		public NegotiationService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
		{
			_context = context;
			_httpContextAccessor = httpContextAccessor;
		}

		public async Task<IEnumerable<Negotiation>> GetNegotiationsAsync()
		{
			return await _context.Negotiations.ToListAsync();
		}

		public async Task<Negotiation> GetNegotiationAsync(int id)
		{
			return await _context.Negotiations.FindAsync(id);
		}

		public async Task<UpdateResultType> UpdateNegotiationAsync(int id, Negotiation Negotiation)
		{
			if (id != Negotiation.Id)
			{
				return UpdateResultType.NotFound;
			}

			_context.Entry(Negotiation).State = EntityState.Modified;

			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateConcurrencyException)
			{
				return UpdateResultType.Conflict;
			}

			return UpdateResultType.Success;
		}

		public async Task<ProposePriceResponse> ProposeNewPriceAsync(int negotiationId, decimal proposedPrice)
		{
			var negotiation = await _context.Negotiations.FindAsync(negotiationId);

			if (negotiation == null)
			{
				return new ProposePriceResponse { Result = ProposePriceResult.NotFound };
			}

			var isUserAssociated = IsUserAssociatedWithNegotiation(negotiationId);

			if (!isUserAssociated)
			{
				return new ProposePriceResponse { Result = ProposePriceResult.Unauthorized };
			}

			Product relevantProduct = await FindRelevantProductAsync(negotiation);

			const int Multiplier = 2;

			if (negotiation.RetriesLeft <= 0)
			{
				return new ProposePriceResponse { Result = ProposePriceResult.IncorrectAction };
			}

			decimal maxAllowedPriceProposition = CalculateMaxAllowedPrice(Multiplier, relevantProduct.Price);
			if (proposedPrice <= 0 || proposedPrice > maxAllowedPriceProposition)
			{
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
				return new ProposePriceResponse { Result = ProposePriceResult.Success };
			}
			catch (DbUpdateException)
			{
				return new ProposePriceResponse { Result = ProposePriceResult.Error };
			}
		}

		public async Task<UpdateResultType> RespondToNegotiationProposalAsync(int negotiationId, bool isApproved)
		{
			var negotiation = await _context.Negotiations.FindAsync(negotiationId);

            if (negotiation == null)
            {
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
			string userId = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

			Negotiation negotiation = new Negotiation(negotiationDetails.ProductId, negotiationDetails.ProposedPrice, userId);

			_context.Negotiations.Add(negotiation);
			await _context.SaveChangesAsync();

			return negotiation;
		}

		public async Task<bool> DeleteNegotiationAsync(int id)
		{
			var negotiation = await _context.Negotiations.FindAsync(id);
			if (negotiation == null)
			{
				return false;
			}

			_context.Negotiations.Remove(negotiation);
			await _context.SaveChangesAsync();

			return true;
		}

		public bool NegotiationExists(int id)
		{
			return _context.Negotiations.Any(e => e.Id == id);
		}

		public string GetLoggedInUserRole()
		{
			var userRole = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value;
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
			var loggedInUserId = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

			return userId == loggedInUserId;
		}

		public async Task<Product> FindRelevantProductAsync(Negotiation negotiation)
		{
			var dbNegotiation = await _context.Negotiations.FirstOrDefaultAsync(e => e.Id == negotiation.Id);
			var productId = dbNegotiation.ProductId;
			Product product = await _context.Products.FindAsync(productId);

			return product;
		}

		public decimal CalculateMaxAllowedPrice(int multiplier, decimal productPrice)
		{
			return multiplier * productPrice;
		}
	}
}
