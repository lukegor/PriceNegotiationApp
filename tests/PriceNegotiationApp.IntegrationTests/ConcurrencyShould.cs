using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.IntegrationTests.Support;
using PriceNegotiationApp.TestKit;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ConcurrencyShould(IntegrationTestFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly string EntityTypeName =
        "PriceNegotiationApp.Modules.Negotiations.Domain.Negotiation, PriceNegotiationApp.Modules.Negotiations";

    [Fact]
    public async Task Second_writer_of_one_negotiation_gets_a_concurrency_exception()
    {
        var negotiationId = await OpenNegotiationAsync();

        // Both writers load the same row (same xmin) before either commits.
        await using var scope1 = fixture.Factory.Services.CreateAsyncScope();
        var db1 = ResolveContext(scope1);
        var first = LoadNegotiation(db1, negotiationId);

        await using var scope2 = fixture.Factory.Services.CreateAsyncScope();
        var db2 = ResolveContext(scope2);
        var second = LoadNegotiation(db2, negotiationId);

        db1.Entry(first).Property("CurrentOffer").CurrentValue = PriceOf(70m);
        await db1.SaveChangesAsync(TestContext.Current.CancellationToken);

        db2.Entry(second).Property("CurrentOffer").CurrentValue = PriceOf(71m);
        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => db2.SaveChangesAsync(TestContext.Current.CancellationToken));

        // The winner's state survives untouched.
        await using var verifyScope = fixture.Factory.Services.CreateAsyncScope();
        var stored = LoadNegotiation(ResolveContext(verifyScope), negotiationId);
        ResolveContext(verifyScope).Entry(stored).Property("CurrentOffer")
            .CurrentValue.ShouldBe(PriceOf(70m));
    }

    /// <summary>
    /// Loads a real tracked aggregate by reflecting over the module-internal DbSet and
    /// invoking FromSqlRaw with the runtime entity type — the domain is invisible to
    /// this assembly, but materialization needs no compile-time reference.
    /// </summary>
    private static object LoadNegotiation(DbContext context, Guid id)
    {
        var entityType = Type.GetType(EntityTypeName)!;
        var dbSet = context.GetType().GetProperty("Negotiations")!.GetValue(context)!;

        var fromSqlRaw = typeof(RelationalQueryableExtensions).GetMethod("FromSqlRaw")!;
        var queryable = (IQueryable)fromSqlRaw.MakeGenericMethod(entityType)
            .Invoke(null, [dbSet,
                "SELECT id, base_price, created_at_utc, current_offer, customer_id, decided_at_utc, "
                + "last_proposal_at_utc, last_staff_action_at_utc, max_proposals, offer_multiplier_limit, "
                + $"product_id, proposals_used, status, xmin FROM negotiations.negotiations WHERE id = '{id}'",
                Array.Empty<object>()])!;

        var results = new List<object>();
        foreach (var entity in queryable)
        {
            results.Add(entity);
        }

        return results.Single();
    }

    private static DbContext ResolveContext(AsyncServiceScope scope)
    {
        var contextType = Type.GetType(
            "PriceNegotiationApp.Modules.Negotiations.Persistence.NegotiationsDbContext, "
            + "PriceNegotiationApp.Modules.Negotiations")!;
        return (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
    }

    private static object PriceOf(decimal value) =>
        Type.GetType("PriceNegotiationApp.Modules.Negotiations.Domain.Price, "
                     + "PriceNegotiationApp.Modules.Negotiations")!
            .GetMethod("From", [typeof(decimal)])!
            .Invoke(null, [value])!;

    private async Task<Guid> OpenNegotiationAsync()
    {
        var staff = await fixture.LoginAsStaffAsync();
        var createProduct = await staff.Client.PostAsJsonAsync("/api/v1/products",
            new { name = Fuzz.NewFaker().ProductName(), price = 100m }, TestContext.Current.CancellationToken);
        createProduct.StatusCode.ShouldBe(HttpStatusCode.Created);
        var product = await createProduct.Content.ReadFromJsonAsync<ProductResponse>(Json, TestContext.Current.CancellationToken);

        var customer = await fixture.CreateUserAsync();
        var open = await customer.Client.PostAsJsonAsync("/api/v1/negotiations",
            new { productId = product!.Id, proposedPrice = 80m }, TestContext.Current.CancellationToken);
        open.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await open.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return created.GetProperty("id").GetGuid();
    }
}
