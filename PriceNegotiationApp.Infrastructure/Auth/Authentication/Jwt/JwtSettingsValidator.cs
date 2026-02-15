using Microsoft.Extensions.Options;

namespace PriceNegotiationApp.Infrastructure.Auth.Authentication.Jwt
{
    public class JwtSettingsValidator : IValidateOptions<JwtSettings>
    {
        public ValidateOptionsResult Validate(string? name, JwtSettings options)
        {
            if (options is null)
            {
                return ValidateOptionsResult.Fail($"JWT configuration is missing.");
            }

            var errors = new List<string>();

            // TODO: Add more validation rules when settings are actually needed

            return errors.Count == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(errors);
        }
    }
}
