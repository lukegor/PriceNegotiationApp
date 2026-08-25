using PriceNegotiationApp.Modules.Identity.Seeding;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.Modules.Identity.Tests;

public class SeedingOptionsValidatorShould
{
    private readonly SeedingOptionsValidator _sut = new();

    private static SeedingOptions Options(
        string adminEmail = "admin@app.com",
        string adminPassword = "Sup3rSecret!",
        string staffEmail = "staff@app.com",
        string staffPassword = "Sup3rSecret!") => new()
    {
        AdminEmail = adminEmail,
        AdminPassword = adminPassword,
        StaffEmail = staffEmail,
        StaffPassword = staffPassword,
    };

    [Fact]
    public void Accept_a_complete_configuration() =>
        _sut.Validate(null, Options()).Succeeded.ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Reject_invalid_admin_email(string? email) =>
        _sut.Validate(null, Options(adminEmail: email!)).Failed.ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    public void Reject_admin_password_shorter_than_identity_floor(string? password) =>
        _sut.Validate(null, Options(adminPassword: password!)).Failed.ShouldBeTrue();

    [Fact]
    public void Aggregate_every_violation_in_one_result()
    {
        // Defaults supply valid emails; only both passwords violate.
        var result = _sut.Validate(null, new SeedingOptions());

        result.Failed.ShouldBeTrue();
        result.Failures.Count().ShouldBe(2);
        result.Failures.ShouldContain(f => f.Contains("AdminPassword"));
        result.Failures.ShouldContain(f => f.Contains("StaffPassword"));
    }
}
