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

        public decimal MaxAllowedPrice { get; private set; }


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        /// <summary>
		/// Empty constructor for EF.
		/// </summary>
		private Negotiation() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        public Negotiation(
            NegotiationId id,
            ProductId productId,
            ProposedPrice proposedPrice,
            CustomerId userId,
            DateTimeOffset timeNow,
            int startingRetries,
            decimal maxAllowedPrice)
        {
            if (proposedPrice.Value > maxAllowedPrice)
            {
                throw new DomainException(
                    $"Proposed price cannot exceed {maxAllowedPrice:C}.");
            }

            Id = id;
            ProductId = productId;
            ProposedPrice = proposedPrice;
            UserId = userId;
            RetriesLeft = startingRetries;
            MaxAllowedPrice = maxAllowedPrice;

            InitializeDefaults(timeNow);
        }

        public void TryNegotiate(ProposedPrice proposedPrice,
            DateTimeOffset timeNow)
        {
            CheckRule(new RetriesLeftMustBePositiveRule(RetriesLeft));

            if (proposedPrice.Value > MaxAllowedPrice)
            {
                throw new DomainException(
                    $"Proposed price cannot exceed {MaxAllowedPrice:C}.");
            }

            ProposedPrice = proposedPrice;
            --RetriesLeft;
            UpdatedAt = timeNow.UtcDateTime;
        }

        public void Close()
        {
            CheckRule(new NegotiationMustBeOpenRule(Status));

            IsAccepted = false;
            Status = NegotiationStatus.Closed;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Archive(bool isApproved, DateTimeOffset updatedAt)
        {
            CheckRule(new NegotiationMustBeOpenRule(Status));

            IsAccepted = isApproved;
            Status = NegotiationStatus.Closed;
            UpdatedAt = DateTime.UtcNow;
        }

        public void ResetRetries(int startingRetries, DateTimeOffset updatedAt)
        {
            CheckRule(new NegotiationMustBeOpenRule(Status));
            RetriesLeft = startingRetries;
            UpdatedAt = updatedAt.UtcDateTime;
        }

        private void InitializeDefaults(DateTimeOffset timeNow)
        {
            IsAccepted = false;
            CreatedAt = timeNow.UtcDateTime;
            UpdatedAt = timeNow.UtcDateTime;
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
