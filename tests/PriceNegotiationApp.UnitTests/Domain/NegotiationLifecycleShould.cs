using Xunit;
using Bogus;
using PriceNegotiationApp.Domain.Exceptions;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.Policy;
using PriceNegotiationApp.Domain.ValueObjects;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.UnitTests.Domain;

public class NegotiationLifecycleShould
{
    private static readonly DefaultNegotiationPolicy Policy = new();
    private readonly Faker _faker = new();
    private readonly Product _product = Product.Create("Widget", Price.From(100m));
    private readonly DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private Negotiation StartValid() =>
        Negotiation.Start(CustomerId.From(_faker.Random.Guid()), _product, Price.From(80m), _now, Policy);

    [Fact]
    public void Start_records_initial_proposal_and_consumes_one_of_three_budgets()
    {
        var negotiation = StartValid();

        Assert.Equal(NegotiationStatus.Open, negotiation.Status);
        Assert.Equal(1, negotiation.ProposalsUsed);
        Assert.Equal(100m, negotiation.BasePrice.Value);
        Assert.Equal(2, negotiation.RemainingProposals(Policy));
    }

    [Fact]
    public void Start_rejects_offer_over_twice_base_price()
    {
        var over = Price.From(201m);

        Assert.Throws<ProposalExceedsLimitException>(
            () => Negotiation.Start(CustomerId.From(_faker.Random.Guid()), _product, over, _now, Policy));
    }

    [Fact]
    public void Start_accepts_offer_exactly_at_limit()
    {
        var atLimit = Price.From(200m);

        var negotiation = Negotiation.Start(CustomerId.From(_faker.Random.Guid()), _product, atLimit, _now, Policy);

        Assert.Equal(200m, negotiation.CurrentOffer.Value);
    }

    [Fact]
    public void CounterPropose_stores_new_offer_within_limit()
    {
        var negotiation = StartValid();

        var outcome = negotiation.CounterPropose(Price.From(90m), _now.AddMinutes(5), Policy);

        Assert.Equal(NegotiationOutcome.CounterProposed, outcome);
        Assert.Equal(90m, negotiation.CurrentOffer.Value);
        Assert.Equal(2, negotiation.ProposalsUsed);
        Assert.Equal(NegotiationStatus.Open, negotiation.Status);
    }

    [Fact]
    public void CounterPropose_over_limit_auto_rejects_and_closes()
    {
        var negotiation = StartValid();

        var outcome = negotiation.CounterPropose(Price.From(500m), _now.AddMinutes(5), Policy);

        Assert.Equal(NegotiationOutcome.AutoRejected, outcome);
        Assert.Equal(NegotiationStatus.Declined, negotiation.Status);
        Assert.NotNull(negotiation.DecidedAtUtc);
    }

    [Fact]
    public void CounterPropose_after_budget_exhaustion_returns_NoProposalsRemaining()
    {
        var negotiation = StartValid();
        negotiation.CounterPropose(Price.From(90m), _now, Policy);
        negotiation.CounterPropose(Price.From(91m), _now, Policy);

        var outcome = negotiation.CounterPropose(Price.From(92m), _now, Policy);

        Assert.Equal(NegotiationOutcome.NoProposalsRemaining, outcome);
        Assert.NotEqual(92m, negotiation.CurrentOffer.Value);
        Assert.Equal(NegotiationStatus.Open, negotiation.Status);
    }

    [Fact]
    public void Accept_closes_negotiation_as_Accepted()
    {
        var negotiation = StartValid();

        negotiation.Accept(_now.AddDays(1));

        Assert.Equal(NegotiationStatus.Accepted, negotiation.Status);
        Assert.NotNull(negotiation.DecidedAtUtc);
    }

    [Fact]
    public void Decline_closes_negotiation_as_Declined()
    {
        var negotiation = StartValid();

        negotiation.Decline(_now.AddDays(1));

        Assert.Equal(NegotiationStatus.Declined, negotiation.Status);
    }

    [Fact]
    public void Terminal_negotiations_refuse_further_operations()
    {
        var negotiation = StartValid();
        negotiation.Accept(_now);

        Assert.Throws<DomainException>(() => negotiation.CounterPropose(Price.From(50m), _now, Policy));
        Assert.Throws<DomainException>(() => negotiation.Accept(_now));
        Assert.Throws<DomainException>(() => negotiation.Decline(_now));
    }
}

