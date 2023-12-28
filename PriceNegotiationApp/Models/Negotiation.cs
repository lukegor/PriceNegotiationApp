using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using System.Security.Claims;

namespace PriceNegotiationApp.Models
{
	public class Negotiation
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }
		[Required]
		[ForeignKey(nameof(ProductId))]
		public string ProductId { get; set; }
		[Required]
		public decimal ProposedPrice { get; set; }
		public bool? IsAccepted { get; set; }
		[Required]
		[Range(0, 2)]
		public int RetriesLeft { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime? UpdatedAt { get; set; }
		[Required]
		public NegotiationStatus Status { get; set; }
		[Required]
		[ForeignKey(nameof(UserId))]
		public string UserId { get; set; }
		//public ApplicationUser User { get; set; }

		public Negotiation(string productId, decimal proposedPrice, string userId)
		{
			ProductId = productId;
			ProposedPrice = proposedPrice;
			UserId = userId;
			InitializeDefaults();
		}

		private void InitializeDefaults()
		{
			IsAccepted = false;
			RetriesLeft = 2; // by creating a negotiation with a certain price, one try is used up (that's why initialized with 2 and not 3 retries)
			CreatedAt = DateTime.UtcNow;
			UpdatedAt = CreatedAt;
			Status = NegotiationStatus.Open;
		}

		public override bool Equals(object? obj)
		{
			if (obj == null || this.GetType() != obj.GetType())
				return false;

			return obj is Negotiation negotiation &&
				   Id == negotiation.Id &&
				   ProductId == negotiation.ProductId &&
				   ProposedPrice == negotiation.ProposedPrice &&
				   IsAccepted == negotiation.IsAccepted &&
				   RetriesLeft == negotiation.RetriesLeft &&
				   CreatedAt == negotiation.CreatedAt &&
				   UpdatedAt == negotiation.UpdatedAt &&
				   Status == negotiation.Status &&
				   UserId == negotiation.UserId;
		}

		public override int GetHashCode()
		{
			HashCode hash = new HashCode();
			hash.Add(Id);
			hash.Add(ProductId);
			hash.Add(ProposedPrice);
			hash.Add(IsAccepted);
			hash.Add(RetriesLeft);
			hash.Add(CreatedAt);
			hash.Add(UpdatedAt);
			hash.Add(Status);
			hash.Add(UserId);
			return hash.ToHashCode();
		}

		public void ProposeNewPrice(decimal proposedPrice)
		{
			--this.RetriesLeft;
			this.ProposedPrice = proposedPrice;
			this.UpdatedAt = DateTime.Now;
		}
	}

	public enum NegotiationStatus
	{
		Open,
		Closed,
		Archived
	}
}
