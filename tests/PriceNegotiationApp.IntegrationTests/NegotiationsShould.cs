using PriceNegotiationApp.IntegrationTests.Support;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class NegotiationsShould(IntegrationTestFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Customer_can_open_negotiation_within_limit()
    {
        var product = await CreateProductAsync();
        var customer = await fixture.CreateUserAsync();

        var response = await customer.Client.PostAsJsonAsync("/api/v1/negotiations",
            new { productId = product.Id, proposedPrice = 80m }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var mine = await customer.Client.GetFromJsonAsync<PagedNegotiations>("/api/v1/negotiations/mine", Json, TestContext.Current.CancellationToken);
        var negotiation = Assert.Single(mine!.Items);
        Assert.Equal("Open", negotiation.Status);
        Assert.Equal(2, negotiation.ProposalsRemaining);
        Assert.Equal(100m, negotiation.BasePrice);
    }

    [Fact]
    public async Task Creation_over_double_base_price_is_rejected_400()
    {
        var product = await CreateProductAsync();
        var customer = await fixture.CreateUserAsync();

        var response = await customer.Client.PostAsJsonAsync("/api/v1/negotiations",
            new { productId = product.Id, proposedPrice = 250m }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("proposal_exceeds_limit", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Second_open_negotiation_for_same_product_conflicts()
    {
        var product = await CreateProductAsync();
        var customer = await fixture.CreateUserAsync();

        var first = await customer.Client.PostAsJsonAsync("/api/v1/negotiations",
            new { productId = product.Id, proposedPrice = 80m }, TestContext.Current.CancellationToken);
        var second = await customer.Client.PostAsJsonAsync("/api/v1/negotiations",
            new { productId = product.Id, proposedPrice = 85m }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("negotiation_already_open", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Full_back_and_forth_then_accept()
    {
        var (customer, staff, negotiationId) = await StartOpenNegotiationAsync();

        // Round 1: staff declines, customer counters
        await StaffDecideAsync(staff, negotiationId, decline: true);
        var counter1 = await CounterProposeAsync(customer, negotiationId, 90m);
        Assert.Equal(HttpStatusCode.OK, counter1.StatusCode);

        // Round 2: staff declines again, customer uses the last proposal
        await StaffDecideAsync(staff, negotiationId, decline: true);
        var counter2 = await CounterProposeAsync(customer, negotiationId, 95m);
        Assert.Equal(HttpStatusCode.OK, counter2.StatusCode);

        // Staff accepts the final offer
        var accept = await staff.Client.PostAsJsonAsync($"/api/v1/negotiations/{negotiationId}/accept", new { }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var view = await GetNegotiationAsync(staff, negotiationId);
        Assert.Equal("Accepted", view.Status);

        // Terminal state refuses further proposals
        var late = await CounterProposeAsync(customer, negotiationId, 50m);
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        var lateBody = await late.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("negotiation_closed", lateBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Budget_exhaustion_yields_409_no_proposals_remaining()
    {
        var (customer, staff, negotiationId) = await StartOpenNegotiationAsync();

        await StaffDecideAsync(staff, negotiationId, decline: true);
        await CounterProposeAsync(customer, negotiationId, 90m);
        await StaffDecideAsync(staff, negotiationId, decline: true);
        await CounterProposeAsync(customer, negotiationId, 91m);
        await StaffDecideAsync(staff, negotiationId, decline: true); // budget now spent

        var third = await CounterProposeAsync(customer, negotiationId, 92m);

        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);
        var body = await third.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("no_proposals_remaining", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Counter_over_limit_auto_rejects_and_closes()
    {
        var (customer, _, negotiationId) = await StartOpenNegotiationAsync();

        var response = await CounterProposeAsync(customer, negotiationId, 500m);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<CounterOutcome>(Json, TestContext.Current.CancellationToken);
        Assert.Equal("AutoRejected", outcome!.Outcome);
        Assert.Equal("Declined", outcome.Negotiation.Status);
        Assert.NotNull(outcome.Negotiation.DecidedAtUtc);
    }

    [Fact]
    public async Task Access_matrix_view_and_counter()
    {
        var (owner, staff, negotiationId) = await StartOpenNegotiationAsync();
        var stranger = await fixture.CreateUserAsync();
        var admin = await fixture.LoginAsAdminAsync();

        // Stranger cannot view or counter-propose
        Assert.Equal(HttpStatusCode.Forbidden,
            (await stranger.Client.GetAsync($"/api/v1/negotiations/{negotiationId}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await CounterProposeAsync(stranger, negotiationId, 50m)).StatusCode);

        // Staff and admin can view
        Assert.Equal(HttpStatusCode.OK,
            (await staff.Client.GetAsync($"/api/v1/negotiations/{negotiationId}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await admin.Client.GetAsync($"/api/v1/negotiations/{negotiationId}", TestContext.Current.CancellationToken)).StatusCode);

        // Only owner can withdraw; admin can delete anything
        Assert.Equal(HttpStatusCode.Forbidden,
            (await stranger.Client.DeleteAsync($"/api/v1/negotiations/{negotiationId}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.DeleteAsync($"/api/v1/negotiations/{negotiationId}", TestContext.Current.CancellationToken)).StatusCode);
    }

    private sealed record CounterOutcome(string Outcome, NegotiationView Negotiation);

    private sealed record NegotiationView(
        Guid Id, Guid ProductId, decimal BasePrice, decimal CurrentOffer, string Status,
        int ProposalsUsed, int ProposalsRemaining, DateTimeOffset CreatedAtUtc,
        DateTimeOffset LastProposalAtUtc, DateTimeOffset? DecidedAtUtc);

    private sealed record PagedNegotiations(IReadOnlyList<NegotiationView> Items, int Page, int PageSize, long TotalCount);

    private async Task<ProductResponse> CreateProductAsync()
    {
        var staff = await fixture.LoginAsStaffAsync();
        var response = await staff.Client.PostAsJsonAsync("/api/v1/products",
            new { name = $"NegProduct{Guid.NewGuid():N}"[..20], price = 100m }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(Json, TestContext.Current.CancellationToken))!;
    }

    private async Task<(UserSession Customer, UserSession Staff, Guid NegotiationId)> StartOpenNegotiationAsync()
    {
        var product = await CreateProductAsync();
        var customer = await fixture.CreateUserAsync();
        var create = await customer.Client.PostAsJsonAsync("/api/v1/negotiations",
            new { productId = product.Id, proposedPrice = 80m }, TestContext.Current.CancellationToken);
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<NegotiationView>(Json, TestContext.Current.CancellationToken);
        return (customer, await fixture.LoginAsStaffAsync(), created!.Id);
    }

    private async Task<HttpResponseMessage> CounterProposeAsync(UserSession customer, Guid id, decimal offer) =>
        await customer.Client.PatchAsJsonAsync($"/api/v1/negotiations/{id}/proposals",
            new { proposedPrice = offer }, TestContext.Current.CancellationToken);

    private async Task StaffDecideAsync(UserSession staff, Guid id, bool decline)
    {
        var route = decline ? "decline" : "accept";
        var response = await staff.Client.PostAsJsonAsync($"/api/v1/negotiations/{id}/{route}", new { }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<NegotiationView> GetNegotiationAsync(UserSession session, Guid id)
    {
        var response = await session.Client.GetAsync($"/api/v1/negotiations/{id}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NegotiationView>(Json, TestContext.Current.CancellationToken))!;
    }
}



