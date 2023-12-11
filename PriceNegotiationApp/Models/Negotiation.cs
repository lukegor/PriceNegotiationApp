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
		public int ProductId { get; set; }
		[Required]
		public decimal ProposedPrice { get; set; }
		public bool IsAccepted { get; set; }
		[Required]
		public int RetriesLeft { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime? UpdatedAt { get; set; }
		[Required]
		public NegotiationStatus Status { get; set; }
		[Required]
		public string UserId { get; set; }
		//public ApplicationUser User { get; set; }

		public Negotiation(int productId, decimal proposedPrice, string userId)
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
	}

	public enum NegotiationStatus
	{
		Open,
		Closed,
		Archived
	}
}
