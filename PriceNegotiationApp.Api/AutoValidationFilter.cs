using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PriceNegotiationApp.Api
{
    public class AutoValidationFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // iterate over controller endpoint's arguments
            foreach (var argument in context.ActionArguments.Values)
            {
                if (argument == null) continue;

                // check if DI has validator for this type of argument
                var argumentType = argument.GetType();
                var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);

                if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
                {
                    continue;
                }

                var validationContext = new ValidationContext<object>(argument);
                var validationResult = await validator.ValidateAsync(validationContext);

                if (!validationResult.IsValid)
                {
                    throw new ValidationException(validationResult.Errors);
                }
            }

            await next();
        }
    }
}
