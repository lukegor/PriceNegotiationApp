using PriceNegotiationApp.Modules.Identity.Infrastructure;
using PriceNegotiationApp.Modules.Identity.Infrastructure.Seeding;
using PriceNegotiationApp.TestKit;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.Modules.Identity.Tests;

public class SeedingOptionsValidatorShould
{
    private readonly SeedingOptionsValidator _sut = new();

    // Unspecified fields fall back to fresh Fuzz values, so every happy-path run
    // exercises different-but-valid data. Invalid partitions stay inline.
    private static SeedingOptions Options(
        string? adminEmail = null,
        string? adminPassword = null,
        string? staffEmail = null,
        string? staffPassword = null) => new()
        {
            AdminEmail = adminEmail ?? Fuzz.Email(),
            AdminPassword = adminPassword ?? Fuzz.Password(),
            StaffEmail = staffEmail ?? Fuzz.Email(),
            StaffPassword = staffPassword ?? Fuzz.Password(),
        };

    [Fact]
    public void Accept_a_complete_configuration_with_generated_values()
    {
        var options = Options();
        Fuzz.Dump("seeding-options", options);

        _sut.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    // Null must be passed as an explicit object-initializer branch: the ?? fallback in
    // Options() would otherwise generate a valid value and silently skip the case.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public void Reject_invalid_admin_email(string email) =>
        _sut.Validate(null, Options(adminEmail: email)).Failed.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public void Reject_invalid_staff_email(string email) =>
        _sut.Validate(null, Options(staffEmail: email)).Failed.ShouldBeTrue();

    [Fact]
    public void Reject_null_admin_email()
    {
        var options = new SeedingOptions
        {
            AdminEmail = null!,
            AdminPassword = Fuzz.Password(),
            StaffEmail = Fuzz.Email(),
            StaffPassword = Fuzz.Password(),
        };

        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")]
    [InlineData("alllowercase123!")]
    [InlineData("ALLUPPERCASE123!")]
    [InlineData("NoDigitsHereOnly!!")]
    [InlineData("NoSymbols12345xY")]
    public void Reject_admin_password_below_strength_floor(string password)
    {
        var options = new SeedingOptions
        {
            AdminEmail = Fuzz.Email(),
            AdminPassword = password,
            StaffEmail = Fuzz.Email(),
            StaffPassword = Fuzz.Password(),
        };

        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Seed123!Apricot!")]
    [InlineData("Str0ng-Passphrase!42")]
    public void Accept_strong_admin_passwords(string password)
    {
        var options = new SeedingOptions
        {
            AdminEmail = Fuzz.Email(),
            AdminPassword = password,
            StaffEmail = Fuzz.Email(),
            StaffPassword = Fuzz.Password(),
        };

        _sut.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Aggregate_every_violation_in_one_result()
    {
        var result = _sut.Validate(null, new SeedingOptions());

        result.Failed.ShouldBeTrue();
        result.Failures.Count().ShouldBe(2);
        result.Failures.ShouldContain(f => f.Contains("AdminPassword"));
        result.Failures.ShouldContain(f => f.Contains("StaffPassword"));
    }
}
