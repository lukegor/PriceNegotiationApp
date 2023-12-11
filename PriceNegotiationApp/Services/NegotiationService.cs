using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Utility;
using System.Security.Claims;

namespace PriceNegotiationApp.Services
{
	public class NegotiationService
	{
		public interface INegotiationService
		{
			Task<IEnumerable<Negotiation>> GetNegotiations();
			Task<Negotiation> GetNegotiation(int id);
			Task<UpdateResultType> UpdateNegotiation(int id, Negotiation Negotiation);
			Task<Negotiation> CreateNegotiation(Negotiation Negotiation);
			Task<bool> DeleteNegotiation(int id);
		}

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

		public async Task<ProposePriceResult> ProposeNewPriceAsync(int negotiationId, decimal proposedPrice)
		{
			var negotiation = await _context.Negotiations.FindAsync(negotiationId);

			if (negotiation == null)
			{
				return ProposePriceResult.NotFound;
			}

			var isUserAssociated = IsUserAssociatedWithNegotiation(negotiationId);

			if (!isUserAssociated)
			{
				return ProposePriceResult.Unauthorized;
			}

			Product relevantProduct = await FindRelevantProductAsync(negotiation);

			const int Multiplier = 2;

			if (negotiation.RetriesLeft <= 0)
			{
				return ProposePriceResult.IncorrectAction;
			}

			if (proposedPrice <= 0 || proposedPrice > Multiplier * relevantProduct.Price)
			{
				return ProposePriceResult.InvalidInput;
			}

			--negotiation.RetriesLeft;
			negotiation.ProposedPrice = proposedPrice;
			negotiation.UpdatedAt = DateTime.Now;

			try
			{
				await _context.SaveChangesAsync();
				return ProposePriceResult.Success;
			}
			catch (DbUpdateException)
			{
				return ProposePriceResult.Error;
			}
		}

		public async Task<Negotiation> AddNegotiationToDbAsync(NegotiationInputModel negotiationDetails)
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

		public bool IsUserAssociatedWithNegotiation(int negotiationId)
		{
			var userId = GetNegotiationAsync(negotiationId).Result.UserId; // Retrieve userId associated with certain negotiation
			var loggedInUserId = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

			return userId == loggedInUserId;
		}

		public async Task<Product> FindRelevantProductAsync(Negotiation negotiation)
		{
			Product product = await _context.Products.FirstOrDefaultAsync(e => e.Id == negotiation.Id);

			return product;
		}
	}
}
