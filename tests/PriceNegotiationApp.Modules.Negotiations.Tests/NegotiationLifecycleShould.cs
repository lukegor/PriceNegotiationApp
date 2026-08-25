using Bogus;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.TestKit;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.Modules.Negotiations.Tests;

public class NegotiationLifecycleShould
{
    private static readonly DefaultNegotiationPolicy Policy = new();
    private readonly Faker _faker = Fuzz.NewFaker();
    private readonly Guid _productId = Guid.CreateVersion7();

    private const decimal BasePrice = 100m;
    private readonly DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private Negotiation StartValid()
    {
        var customerId = CustomerId.From(_faker.Random.Guid());
        Fuzz.Dump("start-valid", new { customer = customerId.Value, product = _productId });
        return Negotiation.Start(customerId, _productId, BasePrice, 80m, _now, Policy);
    }

    [Fact]
    public void Start_records_initial_proposal_snapshots_policy_and_consumes_one_of_three_budgets()
    {
        var negotiation = StartValid();

        negotiation.Status.ShouldBe(NegotiationStatus.Open);
        negotiation.ProposalsUsed.ShouldBe(1);
        negotiation.MaxProposals.ShouldBe(3);
        negotiation.OfferMultiplierLimit.ShouldBe(2.0m);
        negotiation.BasePrice.ShouldBe(100m);
        negotiation.RemainingProposals().ShouldBe(2);
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

        var outcome = negotiation.CounterPropose(90m, _now.AddMinutes(5));

        outcome.ShouldBe(NegotiationOutcome.CounterProposed);
        negotiation.CurrentOffer.ShouldBe(90m);
        negotiation.ProposalsUsed.ShouldBe(2);
        negotiation.Status.ShouldBe(NegotiationStatus.Open);
    }

    [Fact]
    public void CounterPropose_over_limit_auto_rejects_and_closes()
    {
        var negotiation = StartValid();

        var outcome = negotiation.CounterPropose(500m, _now.AddMinutes(5));

        outcome.ShouldBe(NegotiationOutcome.AutoRejected);
        negotiation.Status.ShouldBe(NegotiationStatus.Rejected);
        negotiation.DecidedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public void CounterPropose_uses_limits_snapshotted_at_creation_not_current_config()
    {
        var generousPolicy = new StaticPolicy(maxProposals: 5, multiplierLimit: 3.0m);
        var negotiation = Negotiation.Start(
            CustomerId.From(_faker.Random.Guid()), _productId, BasePrice, 80m, _now, generousPolicy);

        // The DI container now hands out the default (stricter) policy; the aggregate
        // must still obey the rules it was created under.
        var outcome = negotiation.CounterPropose(250m, _now.AddMinutes(5));

        outcome.ShouldBe(NegotiationOutcome.CounterProposed); // legal under 3.0x, illegal under 2.0x
        negotiation.ProposalsUsed.ShouldBe(2);
        negotiation.RemainingProposals().ShouldBe(3);
    }

    [Fact]
    public void CounterPropose_after_budget_exhaustion_returns_NoProposalsRemaining()
    {
        var negotiation = StartValid();
        negotiation.CounterPropose(90m, _now);
        negotiation.CounterPropose(91m, _now);

        var outcome = negotiation.CounterPropose(92m, _now);

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
    public void RejectCurrentOffer_keeps_open_and_stamps_staff_action_without_touching_budget()
    {
        var negotiation = StartValid();

        negotiation.RejectCurrentOffer(_now.AddMinutes(10));

        negotiation.Status.ShouldBe(NegotiationStatus.Open);
        negotiation.LastStaffActionAtUtc.ShouldBe(_now.AddMinutes(10));
        negotiation.ProposalsUsed.ShouldBe(1);
        negotiation.DecidedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void Withdraw_moves_open_negotiation_to_terminal_Withdrawn()
    {
        var negotiation = StartValid();
        negotiation.CounterPropose(90m, _now);

        negotiation.Withdraw(_now.AddHours(1));

        negotiation.Status.ShouldBe(NegotiationStatus.Withdrawn);
        negotiation.DecidedAtUtc.ShouldNotBeNull();
        negotiation.CurrentOffer.ShouldBe(90m); // history preserved
    }

    [Fact]
    public void Terminal_negotiations_refuse_further_operations()
    {
        var withdrawn = StartValid();
        withdrawn.Withdraw(_now);
        var accepted = StartValid();
        accepted.Accept(_now);
        var rejected = StartValid();
        rejected.CounterPropose(500m, _now);

        foreach (var terminal in new[] { withdrawn, accepted, rejected })
        {
            Should.Throw<ClosedNegotiationException>(() => terminal.CounterPropose(50m, _now));
            Should.Throw<ClosedNegotiationException>(() => terminal.Accept(_now));
            Should.Throw<ClosedNegotiationException>(() => terminal.RejectCurrentOffer(_now));
            Should.Throw<ClosedNegotiationException>(() => terminal.Withdraw(_now));
        }
    }

    private sealed class StaticPolicy(int maxProposals, decimal multiplierLimit) : INegotiationPolicy
    {
        public int MaxProposalsPerNegotiation { get; } = maxProposals;

        public decimal ProposalMultiplierLimit { get; } = multiplierLimit;
    }
}
