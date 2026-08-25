using PriceNegotiationApp.IntegrationTests.Support;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using PriceNegotiationApp.TestKit;
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

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var mine = await customer.Client.GetFromJsonAsync<PagedNegotiations>("/api/v1/negotiations/mine", Json, TestContext.Current.CancellationToken);
        var negotiation = mine!.Items.ShouldHaveSingleItem();
        negotiation.Status.ShouldBe("Open");
        negotiation.ProposalsRemaining.ShouldBe(2);
        negotiation.BasePrice.ShouldBe(100m);
    }

    [Fact]
    public async Task Creation_over_double_base_price_is_rejected_422()
    {
        var product = await CreateProductAsync();
        var customer = await fixture.CreateUserAsync();

        var response = await customer.Client.PostAsJsonAsync("/api/v1/negotiations",
            new { productId = product.Id, proposedPrice = 250m }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("proposal_exceeds_limit");
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

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("negotiation_already_open");
    }

    [Fact]
    public async Task Full_back_and_forth_then_accept()
    {
        var (customer, staff, negotiationId) = await StartOpenNegotiationAsync();

        // Round 1: staff rejects the current offer (stays open), customer counters
        var decline1 = await staff.Client.PostAsJsonAsync($"/api/v1/negotiations/{negotiationId}/decline", new { }, TestContext.Current.CancellationToken);
        decline1.StatusCode.ShouldBe(HttpStatusCode.OK);
        var decision1 = await decline1.Content.ReadFromJsonAsync<StaffAction>(Json, TestContext.Current.CancellationToken);
        decision1!.Outcome.ShouldBe("current_offer_rejected");
        decision1.Negotiation.Status.ShouldBe("Open");
        (await CounterProposeAsync(customer, negotiationId, 90m)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Round 2: staff declines again, customer uses the last proposal
        await StaffDecideAsync(staff, negotiationId, decline: true);
        (await CounterProposeAsync(customer, negotiationId, 95m)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Staff accepts the final offer
        var accept = await staff.Client.PostAsJsonAsync($"/api/v1/negotiations/{negotiationId}/accept", new { }, TestContext.Current.CancellationToken);
        accept.StatusCode.ShouldBe(HttpStatusCode.OK);
        var accepted = await accept.Content.ReadFromJsonAsync<StaffAction>(Json, TestContext.Current.CancellationToken);
        accepted!.Outcome.ShouldBe("accepted");

        var view = await GetNegotiationAsync(staff, negotiationId);
        view.Status.ShouldBe("Accepted");

        // Terminal state refuses further proposals
        var late = await CounterProposeAsync(customer, negotiationId, 50m);
        late.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var lateBody = await late.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        lateBody.ShouldContain("negotiation_closed");
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

        third.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await third.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("no_proposals_remaining");
    }

    [Fact]
    public async Task Counter_over_limit_auto_rejects_and_closes()
    {
        var (customer, _, negotiationId) = await StartOpenNegotiationAsync();

        var response = await CounterProposeAsync(customer, negotiationId, 500m);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var outcome = await response.Content.ReadFromJsonAsync<CounterOutcome>(Json, TestContext.Current.CancellationToken);
        outcome!.Outcome.ShouldBe("AutoRejected");
        outcome.Negotiation.Status.ShouldBe("Rejected");
        outcome.Negotiation.DecidedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Access_matrix_view_and_counter()
    {
        var (owner, staff, negotiationId) = await StartOpenNegotiationAsync();
        var stranger = await fixture.CreateUserAsync();
        var admin = await fixture.LoginAsAdminAsync();

        // Stranger cannot view or counter-propose
        (await stranger.Client.GetAsync($"/api/v1/negotiations/{negotiationId}", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CounterProposeAsync(stranger, negotiationId, 50m)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Staff and admin can view
        (await staff.Client.GetAsync($"/api/v1/negotiations/{negotiationId}", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.Client.GetAsync($"/api/v1/negotiations/{negotiationId}", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Owner withdraw soft-closes; admin hard-deletes
        (await stranger.Client.DeleteAsync($"/api/v1/negotiations/{negotiationId}", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await owner.Client.DeleteAsync($"/api/v1/negotiations/{negotiationId}", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Owner_withdraw_closes_but_preserves_history_admin_delete_destroys()
    {
        var (customer, _, negotiationId) = await StartOpenNegotiationAsync();

        var withdraw = await customer.Client.DeleteAsync($"/api/v1/negotiations/{negotiationId}", TestContext.Current.CancellationToken);
        withdraw.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var view = await GetNegotiationAsync(customer, negotiationId);
        view.Status.ShouldBe("Withdrawn");
        view.DecidedAtUtc.ShouldNotBeNull();
        view.BasePrice.ShouldBe(100m); // snapshot history intact

        // Withdrawn is terminal
        var counter = await CounterProposeAsync(customer, negotiationId, 50m);
        counter.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Only an admin can hard-delete; afterwards it is gone
        var admin = await fixture.LoginAsAdminAsync();
        (await admin.Client.DeleteAsync($"/api/v1/negotiations/{negotiationId}", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.Client.GetAsync($"/api/v1/negotiations/{negotiationId}", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Customer_cannot_hard_delete_another_users_negotiation()
    {
        var (_, _, otherId) = await StartOpenNegotiationAsync();
        var stranger = await fixture.CreateUserAsync();

        // stranger is a customer who owns no negotiation here;
        // DELETE must be forbidden, not silently withdraw someone else's deal
        (await stranger.Client.DeleteAsync($"/api/v1/negotiations/{otherId}", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Concurrent_creates_produce_single_winner_and_conflicts_never_500()
    {
        var product = await CreateProductAsync();
        var customer = await fixture.CreateUserAsync();

        var attempts = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            customer.Client.PostAsJsonAsync("/api/v1/negotiations",
                new { productId = product.Id, proposedPrice = 80m }, TestContext.Current.CancellationToken)));

        attempts.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        attempts.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(5);
    }

    [Fact]
    public async Task Negotiations_survive_when_referenced_product_is_deleted()
    {
        var product = await CreateProductAsync();
        var customer = await fixture.CreateUserAsync();

        var create = await customer.Client.PostAsJsonAsync("/api/v1/negotiations",
            new { productId = product.Id, proposedPrice = 80m }, TestContext.Current.CancellationToken);
        create.EnsureSuccessStatusCode();

        var admin = await fixture.LoginAsAdminAsync();
        var delete = await admin.Client.DeleteAsync($"/api/v1/products/{product.Id}", TestContext.Current.CancellationToken);
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var mine = await customer.Client.GetFromJsonAsync<PagedNegotiations>(
            "/api/v1/negotiations/mine?page=1&pageSize=10", Json, TestContext.Current.CancellationToken);
        mine!.TotalCount.ShouldBe(1);
        mine.Items.ShouldHaveSingleItem().BasePrice.ShouldBe(100m);
    }

    [Fact]
    public async Task Ready_endpoint_reports_all_module_schemas()
    {
        var response = await fixture.Anonymous.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("Healthy");
    }

    private sealed record CounterOutcome(string Outcome, NegotiationView Negotiation);

    private sealed record StaffAction(string Outcome, NegotiationView Negotiation);

    private sealed record NegotiationView(
        Guid Id, Guid ProductId, decimal BasePrice, decimal CurrentOffer, string Status,
        int ProposalsUsed, int ProposalsRemaining, DateTimeOffset CreatedAtUtc,
        DateTimeOffset LastProposalAtUtc, DateTimeOffset? DecidedAtUtc);

    private sealed record PagedNegotiations(IReadOnlyList<NegotiationView> Items, int Page, int PageSize, long TotalCount);

    private async Task<ProductResponse> CreateProductAsync()
    {
        var staff = await fixture.LoginAsStaffAsync();
        var response = await staff.Client.PostAsJsonAsync("/api/v1/products",
            new { name = Fuzz.NewFaker().ProductName(), price = 100m }, TestContext.Current.CancellationToken);
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

