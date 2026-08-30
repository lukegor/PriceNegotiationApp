using Microsoft.Extensions.Options;

namespace PriceNegotiationApp.Modules.Identity.Infrastructure.Seeding;

internal sealed class SeedingOptionsValidator : IValidateOptions<SeedingOptions>
{
    public ValidateOptionsResult Validate(string? name, SeedingOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.AdminEmail) || !options.AdminEmail.Contains('@'))
        {
            failures.Add("Seeding:AdminEmail must be a non-empty email address.");
        }

        if (string.IsNullOrWhiteSpace(options.StaffEmail) || !options.StaffEmail.Contains('@'))
        {
            failures.Add("Seeding:StaffEmail must be a non-empty email address.");
        }

        if (string.IsNullOrWhiteSpace(options.AdminPassword) || !IsStrong(options.AdminPassword))
        {
            failures.Add("Seeding:AdminPassword must be at least 12 characters and mix upper-case, lower-case, digit and symbol characters.");
        }

        if (string.IsNullOrWhiteSpace(options.StaffPassword) || !IsStrong(options.StaffPassword))
        {
            failures.Add("Seeding:StaffPassword must be at least 12 characters and mix upper-case, lower-case, digit and symbol characters.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }

    private static bool IsStrong(string password) =>
        password.Length >= 12
        && password.Any(char.IsUpper)
        && password.Any(char.IsLower)
        && password.Any(char.IsDigit)
        && password.Any(c => !char.IsLetterOrDigit(c));
}
