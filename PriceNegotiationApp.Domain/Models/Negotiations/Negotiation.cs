using PriceNegotiationApp.Domain.Models.Abstract;
using PriceNegotiationApp.Domain.Models.Dto;
using PriceNegotiationApp.Domain.Models.Negotiations.Rules;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;
using PriceNegotiationApp.Utility.Utility;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net.Http;
using System.Security.Claims;

namespace PriceNegotiationApp.Domain.Models.Negotiations
{
	public class Negotiation : Entity<string>
	{
		public int ProductId { get; private set; }
		public ProposedPrice ProposedPrice { get; private set; }
		public bool? IsAccepted { get; private set; }
		[Range(0, 2)]
		public int RetriesLeft { get; private set; }
		public DateTime CreatedAt { get; private set; }
		public DateTime UpdatedAt { get; private set; }
		public NegotiationStatus Status { get; private set; }
		[Required]
		[ForeignKey(nameof(UserId))]
		public string UserId { get; set; }
		//public ApplicationUser User { get; set; }

		private const int StartingRetries = 3;
        public const int MaxPriceMultiplier = 2;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        /// <summary>
		/// Empty constructor for EF.
		/// </summary>
		private Negotiation() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        public Negotiation(
			int productId,
			decimal productPrice,
            ProposedPrice proposedPrice,
            string userId)
		{
			ProductId = productId;
			ProposedPrice = proposedPrice;
            UserId = userId;

			InitializeDefaults();
            TryNegotiate(proposedPrice.Value, productPrice);
		}

		private void InitializeDefaults()
		{
            RetriesLeft = StartingRetries;
            IsAccepted = false;
			var timeNow = DateTime.UtcNow;
            CreatedAt = timeNow;
			UpdatedAt = timeNow;
			Status = NegotiationStatus.Open;
		}

        public ProposePriceResponseDto TryNegotiate(decimal proposedPrice, decimal productPrice)
		{
			CheckRule(new ProposedPriceCannotBeNegativeOrZeroRule(proposedPrice));
			CheckRule(new RetriesLeftMustBePositiveRule(RetriesLeft));

            --RetriesLeft;
			UpdatedAt = DateTime.UtcNow;

            decimal maxAllowedPriceProposition = CalculateMaxAllowedPrice(MaxPriceMultiplier, productPrice);

			var result = proposedPrice > maxAllowedPriceProposition
				? ProposePriceResult.Failed
				: ProposePriceResult.Success;

            return new ProposePriceResponseDto
            {
                Result = result,
                MaxAllowedPrice = maxAllowedPriceProposition
            };
        }

		public void Close()
		{
			CheckRule(new NegotiationMustBeOpenRule(Status));

            IsAccepted = false;
            Status = NegotiationStatus.Closed;
            UpdatedAt = DateTime.UtcNow;
        }

		public void Archive(bool isApproved)
		{
            CheckRule(new NegotiationMustBeOpenRule(Status));

            IsAccepted = isApproved;
            Status = NegotiationStatus.Closed;
            UpdatedAt = DateTime.UtcNow;
		}

		public static decimal CalculateMaxAllowedPrice(int multiplier, decimal productPrice)
        {
            return multiplier * productPrice;
        }
    }

	public enum NegotiationStatus
	{
		Open,
		Closed,
		Archived
	}
}
