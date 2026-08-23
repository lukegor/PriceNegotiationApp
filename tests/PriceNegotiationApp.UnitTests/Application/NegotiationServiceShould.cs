using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Exceptions;
using PriceNegotiationApp.Application.Features.Negotiations;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.Policy;
using PriceNegotiationApp.Domain.ValueObjects;
using PriceNegotiationApp.Domain.ValueObjects.Ids;
using Xunit;

namespace PriceNegotiationApp.UnitTests.Application;

public class NegotiationServiceShould
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DefaultNegotiationPolicy Policy = new();

    /// <summary>The caller's persisted customer profile; its domain Id is what negotiations reference.</summary>
    private readonly Customer _customer = Customer.Create(StableIdentityId("u1"));

    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly INegotiationRepository _negotiations = Substitute.For<INegotiationRepository>();
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly NegotiationService _sut;

    public NegotiationServiceShould()
    {
        _customers.GetByIdentityAsync(StableIdentityId("u1"), Arg.Any<CancellationToken>())
            .Returns(_customer);
        _sut = new NegotiationService(
            _negotiations, _products, _customers, Policy, _uow, new FakeTimeProvider(Now));
    }

    [Fact]
    public async Task CreateAsync_throws_NotFound_for_unknown_product()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.CreateAsync(CustomerCaller("u1"), Guid.NewGuid(), 50m, TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.ProductNotFound, exception.Code);
    }

    [Fact]
    public async Task CreateAsync_throws_Conflict_when_open_negotiation_exists()
    {
        var product = GivenProduct();
        _negotiations.FindOpenAsync(product.Id, StableIdentityId("u1"), Arg.Any<CancellationToken>())
            .Returns(StartOpen(product));

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.CreateAsync(CustomerCaller("u1"), product.Id.Value, 50m, TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.NegotiationAlreadyOpen, exception.Code);
    }

    [Fact]
    public async Task CreateAsync_maps_response_with_remaining_proposals()
    {
        var product = GivenProduct();
        _customers.GetOrCreateAsync(StableIdentityId("u1"), Arg.Any<CancellationToken>())
            .Returns(_customer.Id);

        var response = await _sut.CreateAsync(CustomerCaller("u1"), product.Id.Value, 80m, TestContext.Current.CancellationToken);

        Assert.Equal(nameof(NegotiationStatus.Open), response.Status);
        Assert.Equal(2, response.ProposalsRemaining);
        Assert.Equal(1, response.ProposalsUsed);
    }

    [Fact]
    public async Task CounterProposeAsync_returns_outcome_for_owner_within_budget()
    {
        var negotiation = StartOpen(GivenProduct());
        GivenNegotiation(negotiation);

        var outcome = await _sut.CounterProposeAsync(CustomerCaller("u1"), negotiation.Id.Value, 90m, TestContext.Current.CancellationToken);

        Assert.Equal(nameof(NegotiationOutcome.CounterProposed), outcome.Outcome);
        Assert.Equal(90m, outcome.Negotiation.CurrentOffer);
    }

    [Fact]
    public async Task CounterProposeAsync_throws_Forbidden_when_not_owner()
    {
        var negotiation = StartOpen(GivenProduct());
        GivenNegotiation(negotiation);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            _sut.CounterProposeAsync(CustomerCaller("stranger"), negotiation.Id.Value, 90m, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CounterProposeAsync_throws_Conflict_NoProposalsRemaining_when_budget_spent()
    {
        var negotiation = StartOpen(GivenProduct());
        negotiation.CounterPropose(Price.From(90m), Now, Policy);
        negotiation.CounterPropose(Price.From(91m), Now, Policy);
        GivenNegotiation(negotiation);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.CounterProposeAsync(CustomerCaller("u1"), negotiation.Id.Value, 92m, TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.NoProposalsRemaining, exception.Code);
    }

    [Fact]
    public async Task WithdrawAsync_allows_admin_for_any_negotiation()
    {
        var negotiation = StartOpen(GivenProduct());
        _negotiations.GetAsync(negotiation.Id, Arg.Any<CancellationToken>()).Returns(negotiation);

        await _sut.WithdrawAsync(AdminCaller(), negotiation.Id.Value, TestContext.Current.CancellationToken);

        _negotiations.Received().Remove(negotiation);
    }

    private Product GivenProduct()
    {
        var product = Product.Create("Widget", Price.From(100m));
        _products.GetAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        return product;
    }

    private void GivenNegotiation(Negotiation negotiation) =>
        _negotiations.GetAsync(negotiation.Id, Arg.Any<CancellationToken>()).Returns(negotiation);

    private static CallerContext CustomerCaller(string identity) => new(
        StableIdentityId(identity), $"{identity}@test.dev", new HashSet<string> { UserRoles.Customer });

    private static CallerContext AdminCaller() => new(
        Guid.NewGuid(), "admin@test.dev", new HashSet<string> { UserRoles.Admin });

    private Negotiation StartOpen(Product product) =>
        Negotiation.Start(_customer.Id, product, Price.From(80m), Now.AddDays(-1), Policy);

    /// <summary>Deterministic Guid for a given identity string so caller ids match store lookups.</summary>
    private static Guid StableIdentityId(string seed) =>
        new(seed.PadRight(16, '0').Take(16).Select(c => (byte)c).ToArray());
}
