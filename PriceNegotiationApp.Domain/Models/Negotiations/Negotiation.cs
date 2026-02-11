using PriceNegotiationApp.Domain.Models.Abstract;
using PriceNegotiationApp.Domain.Models.Negotiations.Rules;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;
using PriceNegotiationApp.Domain.Models.Products;
using System.ComponentModel.DataAnnotations;

namespace PriceNegotiationApp.Domain.Models.Negotiations
{
    public class Negotiation : Entity<NegotiationId>
    {
        public ProductId ProductId { get; private set; }
        public ProposedPrice ProposedPrice { get; private set; }
        public bool? IsAccepted { get; private set; }
        [Range(0, 2)]
        public int RetriesLeft { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public NegotiationStatus Status { get; private set; }
        public Guid UserId { get; set; }
        //public ApplicationUser User { get; set; }

        private const int StartingRetries = 3;
        public const int MaxPriceMultiplier = 2;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        /// <summary>
		/// Empty constructor for EF.
		/// </summary>
		private Negotiation() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        internal Negotiation(
            NegotiationId id,
            ProductId productId,
            decimal productPrice,
            ProposedPrice proposedPrice,
            Guid userId)
        {
            CheckRule(new ProposedPriceCannotBeNegativeOrZeroRule(proposedPrice.Value));

            Id = id;
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

        public NegotiationResult TryNegotiate(decimal proposedPrice, decimal productPrice)
        {
            CheckRule(new ProposedPriceCannotBeNegativeOrZeroRule(proposedPrice));
            CheckRule(new RetriesLeftMustBePositiveRule(RetriesLeft));

            --RetriesLeft;
            UpdatedAt = DateTime.UtcNow;

            decimal maxAllowedPriceProposition = CalculateMaxAllowedPrice(MaxPriceMultiplier, productPrice);

            if (proposedPrice > maxAllowedPriceProposition)
            {
                return NegotiationResult.Failure(maxAllowedPriceProposition);
            }

            return NegotiationResult.Success(maxAllowedPriceProposition);
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
