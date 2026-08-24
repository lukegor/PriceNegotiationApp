using Bogus;
using PriceNegotiationApp.SharedKernel;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.Modules.Negotiations.Tests;

public class NegotiationLifecycleShould
{
    private static readonly DefaultNegotiationPolicy Policy = new();
    private readonly Faker _faker = new();
    private readonly Guid _productId = Guid.CreateVersion7();

    private const decimal BasePrice = 100m;
    private readonly DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private Negotiation StartValid() =>
        Negotiation.Start(CustomerId.From(_faker.Random.Guid()), _productId, BasePrice, 80m, _now, Policy);

    [Fact]
    public void Start_records_initial_proposal_and_consumes_one_of_three_budgets()
    {
        var negotiation = StartValid();

        negotiation.Status.ShouldBe(NegotiationStatus.Open);
        negotiation.ProposalsUsed.ShouldBe(1);
        negotiation.BasePrice.ShouldBe(100m);
        negotiation.RemainingProposals(Policy).ShouldBe(2);
    }

    [Fact]
    public void Start_rejects_offer_over_twice_base_price() =>
        Should.Throw<ProposalExceedsLimitException>(
            () => Negotiation.Start(CustomerId.From(_faker.Random.Guid()), _productId, BasePrice, 201m, _now, Policy));

    [Fact]
    public void Start_accepts_offer_exactly_at_limit()
    {
        var negotiation = Negotiation.Start(CustomerId.From(_faker.Random.Guid()), _productId, BasePrice, 200m, _now, Policy);

        negotiation.CurrentOffer.ShouldBe(200m);
    }

    [Fact]
    public void CounterPropose_stores_new_offer_within_limit()
    {
        var negotiation = StartValid();

        var outcome = negotiation.CounterPropose(90m, _now.AddMinutes(5), Policy);

        outcome.ShouldBe(NegotiationOutcome.CounterProposed);
        negotiation.CurrentOffer.ShouldBe(90m);
        negotiation.ProposalsUsed.ShouldBe(2);
        negotiation.Status.ShouldBe(NegotiationStatus.Open);
    }

    [Fact]
    public void CounterPropose_over_limit_auto_rejects_and_closes()
    {
        var negotiation = StartValid();

        var outcome = negotiation.CounterPropose(500m, _now.AddMinutes(5), Policy);

        outcome.ShouldBe(NegotiationOutcome.AutoRejected);
        negotiation.Status.ShouldBe(NegotiationStatus.Declined);
        negotiation.DecidedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public void CounterPropose_after_budget_exhaustion_returns_NoProposalsRemaining()
    {
        var negotiation = StartValid();
        negotiation.CounterPropose(90m, _now, Policy);
        negotiation.CounterPropose(91m, _now, Policy);

        var outcome = negotiation.CounterPropose(92m, _now, Policy);

        outcome.ShouldBe(NegotiationOutcome.NoProposalsRemaining);
        negotiation.CurrentOffer.ShouldNotBe(92m);
        negotiation.Status.ShouldBe(NegotiationStatus.Open);
    }

    [Fact]
    public void Accept_closes_negotiation_as_Accepted()
    {
        var negotiation = StartValid();

        negotiation.Accept(_now.AddDays(1));

        negotiation.Status.ShouldBe(NegotiationStatus.Accepted);
        negotiation.DecidedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public void Decline_keeps_open_so_customer_can_counter()
    {
        var negotiation = StartValid();

        negotiation.Decline();

        negotiation.Status.ShouldBe(NegotiationStatus.Open);
    }

    [Fact]
    public void Terminal_negotiations_refuse_further_operations()
    {
        var negotiation = StartValid();
        negotiation.Accept(_now);

        Should.Throw<ClosedNegotiationException>(() => negotiation.CounterPropose(50m, _now, Policy));
        Should.Throw<ClosedNegotiationException>(() => negotiation.Accept(_now));
        Should.Throw<ClosedNegotiationException>(() => negotiation.Decline());
    }
}


