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
		[ForeignKey(nameof(Product))]
		public string ProductId { get; set; }
		[Required]
		public decimal ProposedPrice { get; set; }
		public bool? IsAccepted { get; set; }
		[Required]
		public int RetriesLeft { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime? UpdatedAt { get; set; }
		[Required]
		public NegotiationStatus Status { get; set; }
		[Required]
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
			RetriesLeft = 2;
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
	}

	public enum NegotiationStatus
	{
		Open,
		Closed,
		Archived
	}
}
