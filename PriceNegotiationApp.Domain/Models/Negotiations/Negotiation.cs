using PriceNegotiationApp.Domain.Models.Abstract;
using PriceNegotiationApp.Domain.Models.Customer;
using PriceNegotiationApp.Domain.Models.Negotiations.Rules;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;
using PriceNegotiationApp.Domain.Models.Products;
using System.ComponentModel.DataAnnotations;

namespace PriceNegotiationApp.Domain.Models.Negotiations
{
    public class Negotiation : Entity<NegotiationId>
    {
        /// <summary>
        /// Id of <see cref="Product" associated with the negotiation/>
        /// </summary>
        public ProductId ProductId { get; private set; }

        /// <summary>
        /// Last price proposed by the user in the negotiation process
        /// </summary>
        public ProposedPrice ProposedPrice { get; private set; }

        public bool? IsAccepted { get; private set; }

        [Range(0, 2)]
        public int RetriesLeft { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public DateTime UpdatedAt { get; private set; }

        public NegotiationStatus Status { get; private set; }

        public CustomerId UserId { get; set; }


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
            CustomerId userId)
        {
            Id = id;
            ProductId = productId;
            ProposedPrice = proposedPrice;
            UserId = userId;

            InitializeDefaults();
            TryNegotiate(proposedPrice, productPrice);
        }

        public NegotiationResult TryNegotiate(ProposedPrice proposedPrice, decimal productPrice)
        {
            CheckRule(new RetriesLeftMustBePositiveRule(RetriesLeft));

            --RetriesLeft;
            UpdatedAt = DateTime.UtcNow;

            decimal maxAllowedPriceProposition = CalculateMaxAllowedPrice(MaxPriceMultiplier, productPrice);

            if (proposedPrice.Value > maxAllowedPriceProposition)
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

        private void InitializeDefaults()
        {
            RetriesLeft = StartingRetries;
            IsAccepted = false;
            var timeNow = DateTime.UtcNow;
            CreatedAt = timeNow;
            UpdatedAt = timeNow;
            Status = NegotiationStatus.Open;
        }
    }

    public record NegotiationStatus(string Value)
    {
        public static readonly NegotiationStatus Open = new("Open");
        public static readonly NegotiationStatus Closed = new("Closed");
        public static readonly NegotiationStatus Archived = new("Archived");
    }
}
