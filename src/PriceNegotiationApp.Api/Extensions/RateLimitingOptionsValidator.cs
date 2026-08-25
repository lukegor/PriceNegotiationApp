using Microsoft.Extensions.Options;

namespace PriceNegotiationApp.Api.Extensions;

public sealed class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>
{
    public ValidateOptionsResult Validate(string? name, RateLimitingOptions options) =>
        options.AuthPermitLimit >= 1
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{RateLimitingOptions.SectionName}:AuthPermitLimit must be >= 1.");
}
