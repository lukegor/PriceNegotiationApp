using PriceNegotiationApp.IntegrationTests.Support;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using PriceNegotiationApp.TestKit;
using System.Text.Json;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ProductsShould(IntegrationTestFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Anonymous_can_list_and_get_but_not_write()
    {
        var staff = await fixture.LoginAsStaffAsync();
        var created = await CreateProductAsync(staff);

        var list = await fixture.Anonymous.GetAsync("/api/v1/products?page=1&pageSize=10", TestContext.Current.CancellationToken);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);

        var single = await fixture.Anonymous.GetAsync($"/api/v1/products/{created.Id}", TestContext.Current.CancellationToken);
        single.StatusCode.ShouldBe(HttpStatusCode.OK);

        var post = await fixture.Anonymous.PostAsJsonAsync("/api/v1/products",
            DenialPayload(1m), TestContext.Current.CancellationToken);
        post.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var put = await fixture.Anonymous.PutAsJsonAsync($"/api/v1/products/{created.Id}",
            DenialPayload(2m), TestContext.Current.CancellationToken);
        put.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var delete = await fixture.Anonymous.DeleteAsync($"/api/v1/products/{created.Id}", TestContext.Current.CancellationToken);
        delete.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Customer_blocked_from_all_writes()
    {
        var customer = await fixture.CreateUserAsync();

        (await customer.Client.PostAsJsonAsync("/api/v1/products",
            DenialPayload(1m), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await customer.Client.PutAsJsonAsync($"/api/v1/products/{Guid.NewGuid()}",
            DenialPayload(1m), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await customer.Client.DeleteAsync(
            $"/api/v1/products/{Guid.NewGuid()}", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Staff_can_update_but_not_delete()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var created = await CreateProductAsync(admin);
        var staff = await fixture.LoginAsStaffAsync();

        var put = await staff.Client.PutAsJsonAsync($"/api/v1/products/{created.Id}",
            new { name = Fuzz.NewFaker().ProductName(), price = created.Price + 1 }, TestContext.Current.CancellationToken);
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var delete = await staff.Client.DeleteAsync($"/api/v1/products/{created.Id}", TestContext.Current.CancellationToken);
        delete.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_can_delete()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var created = await CreateProductAsync(admin);

        var delete = await admin.Client.DeleteAsync($"/api/v1/products/{created.Id}", TestContext.Current.CancellationToken);
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var get = await fixture.Anonymous.GetAsync($"/api/v1/products/{created.Id}", TestContext.Current.CancellationToken);
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Missing_product_returns_404_with_stable_code()
    {
        var response = await fixture.Anonymous.GetAsync(
            $"/api/v1/products/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("product_not_found");
    }

    [Fact]
    public async Task Domain_rejects_invalid_product_payloads_with_422()
    {
        var staff = await fixture.LoginAsStaffAsync();

        var emptyName = await staff.Client.PostAsJsonAsync("/api/v1/products",
            new { name = string.Empty, price = Fuzz.NewFaker().Price() }, TestContext.Current.CancellationToken);
        emptyName.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var emptyNameBody = await emptyName.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        emptyNameBody.ShouldContain("domain_rule_violated");

        var negativePrice = await staff.Client.PostAsJsonAsync("/api/v1/products",
            new { name = Fuzz.NewFaker().ProductName(), price = -5m }, TestContext.Current.CancellationToken);
        negativePrice.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var negativeBody = await negativePrice.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        negativeBody.ShouldContain("validation_failed");
    }

    [Fact]
    public async Task Filtering_sorting_and_paging_work()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var marker = $"ZProbe{Guid.NewGuid():N}"[..12];
        await CreateProductAsync(admin, $"{marker} Alpha", 10m);
        await CreateProductAsync(admin, $"{marker} Beta", 30m);
        await CreateProductAsync(admin, $"{marker} Gamma", 20m);

        var search = await fixture.Anonymous.GetAsync(
            $"/api/v1/products?search={marker}", TestContext.Current.CancellationToken);
        var paged = await search.Content.ReadFromJsonAsync<PagedProducts>(Json, TestContext.Current.CancellationToken);
        paged!.TotalCount.ShouldBe(3);

        var sorted = await fixture.Anonymous.GetAsync(
            $"/api/v1/products?search={marker}&sortBy=price&sortDesc=true&page=1&pageSize=2",
            TestContext.Current.CancellationToken);
        sorted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sortedPage = await sorted.Content.ReadFromJsonAsync<PagedProducts>(Json, TestContext.Current.CancellationToken);
        sortedPage!.TotalCount.ShouldBe(3);
        sortedPage.Items.Count.ShouldBe(2);
        sortedPage.Items[0].Price.ShouldBeGreaterThanOrEqualTo(sortedPage.Items[1].Price);
        sortedPage.Items[0].Price.ShouldBe(30m);

        var range = await fixture.Anonymous.GetAsync(
            $"/api/v1/products?search={marker}&minPrice=15&maxPrice=25",
            TestContext.Current.CancellationToken);
        var rangePage = await range.Content.ReadFromJsonAsync<PagedProducts>(Json, TestContext.Current.CancellationToken);
        var item = rangePage!.Items.ShouldHaveSingleItem();
        item.Price.ShouldBe(20m);
    }

    [Fact]
    public async Task Put_with_identical_body_is_idempotent()
    {
        var staff = await fixture.LoginAsStaffAsync();
        var created = await CreateProductAsync(staff);

        var put = await staff.Client.PutAsJsonAsync($"/api/v1/products/{created.Id}",
            new { name = created.Name, price = created.Price }, TestContext.Current.CancellationToken);

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await put.Content.ReadFromJsonAsync<ProductResponse>(Json, TestContext.Current.CancellationToken);
        body!.Name.ShouldBe(created.Name);
    }

    private static object DenialPayload(decimal price) => new
    {
        name = Fuzz.NewFaker().ProductName(),
        price,
    };

    private static async Task<ProductResponse> CreateProductAsync(UserSession session, string? name = null, decimal? price = null)
    {
        var response = await session.Client.PostAsJsonAsync("/api/v1/products",
            new { name = name ?? Fuzz.NewFaker().ProductName(), price = price ?? Fuzz.NewFaker().Price() }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(Json, TestContext.Current.CancellationToken))!;
    }
}


