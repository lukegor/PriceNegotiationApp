using Microsoft.Extensions.Options;

namespace PriceNegotiationApp.Modules.Identity.Auth;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();
        if (options.SecretKey.Length < 32)
        {
            failures.Add("Jwt:SecretKey must be at least 32 characters.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience is required.");
        }

        if (options.ExpiryMinutes < 1)
        {
            failures.Add("Jwt:ExpiryMinutes must be >= 1.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}

