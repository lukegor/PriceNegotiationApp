using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.BuildingBlocks;
using NSubstitute;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Application.Features.Products;
using PriceNegotiationApp.Domain.Models;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.UnitTests.Application;

public class ProductServiceShould
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ProductService _sut;

    public ProductServiceShould()
    {
        _sut = new ProductService(_products, _uow);
    }

    [Fact]
    public async Task GetAsync_throws_NotFound_when_missing()
    {
        var exception = await Should.ThrowAsync<NotFoundException>(
            () => _sut.GetAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(LegacyErrorCodes.ProductNotFound);
    }

    [Fact]
    public async Task CreateAsync_persists_and_maps_response()
    {
        Product? added = null;
        await _products.AddAsync(Arg.Do<Product>(p => added = p), Arg.Any<CancellationToken>());

        var response = await _sut.CreateAsync("  Keyboard ", 99.5m, TestContext.Current.CancellationToken);

        response.Name.ShouldBe("Keyboard");
        added!.Name.ShouldBe("Keyboard");
        await _uow.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_applies_changes_to_existing_product()
    {
        var product = Product.Create("Old", 10m);
        _products.GetAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);

        var response = await _sut.UpdateAsync(product.Id.Value, "New", 20m, TestContext.Current.CancellationToken);

        response.Name.ShouldBe("New");
        response.Price.ShouldBe(20m);
    }

    [Fact]
    public async Task DeleteAsync_removes_existing_product()
    {
        var product = Product.Create("Doomed", 1m);
        _products.GetAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);

        await _sut.DeleteAsync(product.Id.Value, TestContext.Current.CancellationToken);

        _products.Received().Remove(product);
    }
}





