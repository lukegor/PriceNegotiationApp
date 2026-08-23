using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Exceptions;
using PriceNegotiationApp.Domain.Exceptions;
using Vogen;

namespace PriceNegotiationApp.Api;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        var (status, title, code) = exception switch
        {
            ProposalExceedsLimitException => (StatusCodes.Status400BadRequest, "Proposal rejected", ErrorCodes.ProposalExceedsLimit),
            ValueObjectValidationException => (StatusCodes.Status400BadRequest, "Invalid value", ErrorCodes.ValidationFailed),
            InvalidRequestException invalidRequest => (StatusCodes.Status400BadRequest, "Invalid request", invalidRequest.Code),
            ClosedNegotiationException => (StatusCodes.Status409Conflict, "Business rule violated", ErrorCodes.NegotiationClosed),
            DomainException => (StatusCodes.Status400BadRequest, "Business rule violated", ErrorCodes.DomainRuleViolated),
            NotFoundException notFound => (StatusCodes.Status404NotFound, "Resource not found", notFound.Code),
            ConflictException conflict => (StatusCodes.Status409Conflict, "Conflict", conflict.Code),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden, "Forbidden", ErrorCodes.Forbidden),
            UnauthorizedException unauthorized => (StatusCodes.Status401Unauthorized, "Authentication failed", unauthorized.Code),
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested
                => (499, "Request cancelled", "client_closed_request"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error", ErrorCodes.InternalError),
        };

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = environment.IsDevelopment() && exception is not OperationCanceledException ? exception.Message : null,
                Extensions = { ["code"] = code },
            },
        });
    }
}
