namespace PriceNegotiationApp.Models
{
	public class Negotiation
	{
		public int Id { get; set; }
		public int ProductId { get; set; }
		public decimal ProposedPrice { get; set; }
		public bool IsAccepted { get; set; }
		public int RetriesLeft { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime? UpdatedAt { get; set; }
		public string Status { get; set; }
		public string UserId { get; set; }
		//public ApplicationUser User { get; set; }
	}

	public enum NegotiationStatus
	{
		Open,
		Close,
		Archived
	}
}
