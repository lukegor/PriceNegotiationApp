using FluentValidation;

namespace PriceNegotiationApp.Application
{
    public static class ValidationExtensions
    {
        extension<T>(IValidator<T> validator)
        {
            public async Task EnsureValidAsync(T instance)
            {
                var result = await validator.ValidateAsync(instance);
                if (!result.IsValid)
                {
                    throw new ValidationException(result.Errors);
                }
            }
        }
    }
}
