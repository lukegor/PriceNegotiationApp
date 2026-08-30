using Microsoft.Extensions.Options;

namespace PriceNegotiationApp.Modules.Identity.Infrastructure;

internal sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.PrivateKey))
        {
            failures.Add("Jwt:PrivateKey is required (ES256 PKCS#8 PEM; malformed keys fail at startup with generation instructions).");
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

