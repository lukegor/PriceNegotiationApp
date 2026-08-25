using Microsoft.Extensions.Options;

namespace PriceNegotiationApp.Modules.Identity.Seeding;

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

        if (string.IsNullOrWhiteSpace(options.AdminPassword) || options.AdminPassword.Length < 8)
        {
            failures.Add("Seeding:AdminPassword must be at least 8 characters.");
        }

        if (string.IsNullOrWhiteSpace(options.StaffPassword) || options.StaffPassword.Length < 8)
        {
            failures.Add("Seeding:StaffPassword must be at least 8 characters.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
