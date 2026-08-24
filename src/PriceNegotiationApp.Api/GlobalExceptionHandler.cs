using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;
using PriceNegotiationApp.SharedKernel;
using Vogen;

namespace PriceNegotiationApp.Api;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    // HTTP status semantics used below:
    //   400 — the request could not be understood (handled by framework binding; nothing maps here).
    //   401/403/404 — authentication, authorization, missing resource.
    //   409 — well-formed request that conflicts with the current persistent state.
    //   422 — well-formed request whose payload fails input/business validation.
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        var (status, title, code) = exception switch
        {
            // 422 Unprocessable Content — payload fails validation or business input rules
            ProposalExceedsLimitException => (StatusCodes.Status422UnprocessableEntity, "Proposal rejected", NegotiationErrorCodes.ProposalExceedsLimit),
            ValueObjectValidationException => (StatusCodes.Status422UnprocessableEntity, "Invalid value", ErrorCodes.ValidationFailed),
            InvalidRequestException invalidRequest => (StatusCodes.Status422UnprocessableEntity, "Invalid request", invalidRequest.Code),

            // 409 Conflict — request collides with current persistent state
            ConflictException conflict => (StatusCodes.Status409Conflict, "Conflict", conflict.Code),
            ClosedNegotiationException => (StatusCodes.Status409Conflict, "Business rule violated", NegotiationErrorCodes.NegotiationClosed),

            // remaining domain exceptions are input-validation failures
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Business rule violated", ErrorCodes.DomainRuleViolated),

            NotFoundException notFound => (StatusCodes.Status404NotFound, "Resource not found", notFound.Code),
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







