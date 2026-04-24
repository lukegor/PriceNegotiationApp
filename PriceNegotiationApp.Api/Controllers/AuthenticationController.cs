using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PriceNegotiationApp.Application.Services;
using PriceNegotiationApp.Contracts.Identities.Dtos.Requests;
using PriceNegotiationApp.Contracts.Identities.Dtos.Responses;
using PriceNegotiationApp.Presentation.Identities.Mappers;

namespace PriceNegotiationApp.Api.Controllers
{
    [ApiController]
    public class AuthenticationController(
        IAuthService authService) : ControllerBase
    {
        [HttpPost("Login")]
        [EndpointDescription("Log into an account")]
        [AllowAnonymous]
        public async Task<Results<Ok<AuthResponseDto>, BadRequest<AuthResponseDto>>> Login(
            [FromBody] LoginRequestDto request)
        {
            var authResult = await authService.AuthenticateAsync(request.ToCommand());

            if (!authResult.IsAuthSuccessful)
            {
                return TypedResults.BadRequest(authResult.ToResponseDto());
            }

            return TypedResults.Ok(authResult.ToResponseDto());
        }

        [HttpPost("Registration")]
        [EndpointDescription("Registers a new user account")]
        [AllowAnonymous]
        public async Task<Results<Created, BadRequest<IEnumerable<string>>>> RegisterUser(
            [FromBody] RegisterUserRequestDto request)
        {
            var result = await authService.RegisterUserAsync(request.ToCommand());

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return TypedResults.BadRequest(errors);
            }

            return TypedResults.Created();
        }
    }
}
