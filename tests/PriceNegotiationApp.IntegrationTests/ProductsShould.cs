using PriceNegotiationApp.IntegrationTests.Support;
using System.Net;
using System.Net.Http.Json;
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
        var create = await staff.Client.PostAsJsonAsync("/api/v1/products",
            new { name = "Anon Probe", price = 42m }, TestContext.Current.CancellationToken);
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<ProductResponse>(Json, TestContext.Current.CancellationToken);

        var list = await fixture.Anonymous.GetAsync("/api/v1/products?page=1&pageSize=10", TestContext.Current.CancellationToken);
        var listBody = await list.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Console.WriteLine("LIST400BODY=" + listBody);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var single = await fixture.Anonymous.GetAsync($"/api/v1/products/{created!.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, single.StatusCode);

        var post = await fixture.Anonymous.PostAsJsonAsync("/api/v1/products",
            new { name = "X", price = 1m }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);

        var put = await fixture.Anonymous.PutAsJsonAsync($"/api/v1/products/{created.Id}",
            new { name = "X", price = 2m }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);

        var delete = await fixture.Anonymous.DeleteAsync($"/api/v1/products/{created.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
    }

    [Fact]
    public async Task Customer_blocked_from_all_writes()
    {
        var customer = await fixture.CreateUserAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.Client.PostAsJsonAsync("/api/v1/products",
            new { name = "C", price = 1m }, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.Client.PutAsJsonAsync($"/api/v1/products/{Guid.NewGuid()}",
            new { name = "C", price = 1m }, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.Client.DeleteAsync(
            $"/api/v1/products/{Guid.NewGuid()}", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Staff_can_update_but_not_delete()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var created = await CreateProductAsync(admin);
        var staff = await fixture.LoginAsStaffAsync();

        var put = await staff.Client.PutAsJsonAsync($"/api/v1/products/{created.Id}",
            new { name = "Staff Updated", price = created.Price + 1 }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var delete = await staff.Client.DeleteAsync($"/api/v1/products/{created.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Admin_can_delete()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var created = await CreateProductAsync(admin);

        var delete = await admin.Client.DeleteAsync($"/api/v1/products/{created.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var get = await fixture.Anonymous.GetAsync($"/api/v1/products/{created.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Missing_product_returns_404_with_stable_code()
    {
        var response = await fixture.Anonymous.GetAsync(
            $"/api/v1/products/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("product_not_found", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_create_payload_fails_validation()
    {
        var staff = await fixture.LoginAsStaffAsync();

        var response = await staff.Client.PostAsJsonAsync("/api/v1/products",
            new { name = string.Empty, price = -5m }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("errors", body.ToLowerInvariant(), StringComparison.Ordinal);
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
        Assert.Equal(3, paged!.TotalCount);

        var sorted = await fixture.Anonymous.GetAsync(
            $"/api/v1/products?search={marker}&sortBy=price&sortDesc=true&page=1&pageSize=2",
            TestContext.Current.CancellationToken);
        var sortedPage = await sorted.Content.ReadFromJsonAsync<PagedProducts>(Json, TestContext.Current.CancellationToken);
        Assert.Equal(3, sortedPage!.TotalCount);
        Assert.Equal(2, sortedPage.Items.Count);
        Assert.True(sortedPage.Items[0].Price >= sortedPage.Items[1].Price);
        Assert.Equal(30m, sortedPage.Items[0].Price);

        var range = await fixture.Anonymous.GetAsync(
            $"/api/v1/products?search={marker}&minPrice=15&maxPrice=25",
            TestContext.Current.CancellationToken);
        var rangePage = await range.Content.ReadFromJsonAsync<PagedProducts>(Json, TestContext.Current.CancellationToken);
        var item = Assert.Single(rangePage!.Items);
        Assert.Equal(20m, item.Price);
    }

    [Fact]
    public async Task Put_with_identical_body_is_idempotent()
    {
        var staff = await fixture.LoginAsStaffAsync();
        var created = await CreateProductAsync(staff);

        var put = await staff.Client.PutAsJsonAsync($"/api/v1/products/{created.Id}",
            new { name = created.Name, price = created.Price }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var body = await put.Content.ReadFromJsonAsync<ProductResponse>(Json, TestContext.Current.CancellationToken);
        Assert.Equal(created.Name, body!.Name);
    }

    private static async Task<ProductResponse> CreateProductAsync(UserSession session, string? name = null, decimal? price = null)
    {
        var response = await session.Client.PostAsJsonAsync("/api/v1/products",
            new { name = name ?? "Matrix Product", price = price ?? 50m }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(Json, TestContext.Current.CancellationToken))!;
    }
}






