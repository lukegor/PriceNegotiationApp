using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PriceNegotiationApp.Models
{
	public class Negotiation
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }
		public int ProductId { get; set; }
		public decimal ProposedPrice { get; set; }
		public bool IsAccepted { get; set; }
		public int RetriesLeft { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime? UpdatedAt { get; set; }
		public NegotiationStatus Status { get; set; }
		public string UserId { get; set; }
		//public ApplicationUser User { get; set; }

		public Negotiation(int productId, decimal proposedPrice, string userId)
		{
			ProductId = productId;
			ProposedPrice = proposedPrice;
			IsAccepted = false;
			RetriesLeft = 3;
			CreatedAt = DateTime.UtcNow;
			UpdatedAt = CreatedAt;
			Status = NegotiationStatus.Open;
			UserId = userId;
		}
	}

	public enum NegotiationStatus
	{
		Open,
		Closed,
		Archived
	}
}
