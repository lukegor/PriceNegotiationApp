using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PriceNegotiationApp.Application.Common.Exceptions;
using PriceNegotiationApp.Domain;
using System.Security.Authentication;

namespace PriceNegotiationApp.Api
{
    public class GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            logger.LogError(exception, "Wyjątek podczas przetwarzania żądania: {Message}", exception.Message);

            if (exception is OperationCanceledException)
            {
                httpContext.Response.StatusCode = 499; // Client Closed Request
                return true;
            }

            var statusCode = exception switch
            {
                ArgumentException or InvalidOperationException or DomainException or ValidationException
                    => StatusCodes.Status400BadRequest,
                AuthenticationException => StatusCodes.Status401Unauthorized,
                UnauthorizedAccessException => StatusCodes.Status403Forbidden,
                NotFoundException => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status500InternalServerError
            };

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = exception.GetType().Name,
                Detail = exception.Message,
            };

            httpContext.Response.StatusCode = statusCode;

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true;
        }
    }
}
