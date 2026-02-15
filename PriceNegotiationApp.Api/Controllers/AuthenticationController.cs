using FluentValidation;
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
        IAuthService authService, IValidator<LoginRequestDto> validator1) : ControllerBase
    {
        private readonly IAuthService _authService = authService;

        /// <summary>Log into an account</summary>
        /// <returns>Returns true if login is successful, false otherwise.</returns>
        [HttpPost("Login")]
        [AllowAnonymous]
        public async Task<Results<Ok<AuthResponseDto>, BadRequest<AuthResponseDto>>> Login(
            [FromBody] LoginRequestDto request)
        {
            var authResult = await _authService.AuthenticateAsync(request.ToCommand());

            if (!authResult.IsAuthSuccessful)
            {
                return TypedResults.BadRequest(authResult.ToResponseDto());
            }

            return TypedResults.Ok(authResult.ToResponseDto());
        }

        /// <summary>
        /// Registers a new user.
        /// </summary>
        /// <returns>Returns a 201 Created response if successful.</returns>
        [HttpPost("Registration")]
        [AllowAnonymous]
        public async Task<Results<CreatedAtRoute<object>, BadRequest<IEnumerable<string>>>> RegisterUser(
            [FromBody] RegisterUserRequestDto request)
        {
            var result = await _authService.RegisterUserAsync(request.ToCommand());

            if (result.Succeeded)
            {
                object responseBody = new { Message = "User registration successful" };
                return TypedResults.CreatedAtRoute(responseBody, nameof(RegisterUser), new { userName = request.UserName });
            }

            var errors = result.Errors.Select(e => e.Description);
            return TypedResults.BadRequest(errors);
        }
    }
}
