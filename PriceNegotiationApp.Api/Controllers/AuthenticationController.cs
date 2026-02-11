using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PriceNegotiationApp.Application.Common.Identities.Dtos.Requests.Login;
using PriceNegotiationApp.Application.Common.Identities.Dtos.Requests.RegisterUser;
using PriceNegotiationApp.Application.Services;

namespace PriceNegotiationApp.Api.Controllers
{
    [ApiController]
    public class AuthenticationController(IAuthService authService) : ControllerBase
    {
        private readonly IAuthService _authService = authService;

        /// <summary>Log into an account</summary>
        /// <param name="model">username and password</param>
        /// <returns>Returns true if login is successful, false otherwise.</returns>
        [HttpPost("Login")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto model)
        {
            var authResponse = await _authService.AuthenticateAsync(model);

            if (!authResponse.IsAuthSuccessful)
                return Unauthorized(authResponse);

            return Ok(authResponse);
        }

        /// <summary>
        /// Registers a new user.
        /// </summary>
        /// <param name="userForRegistration">The data required for user registration.</param>
        /// <returns>
        /// Returns a 201 Created response if successful,
        /// or a a 400 Bad Request response with details of the validation errors or registration failure.
        /// </returns>
        [HttpPost("Registration")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RegisterUser([FromBody] RegisterUserRequestDto userForRegistration)
        {
            var result = await _authService.RegisterUserAsync(userForRegistration);

            if (result.Succeeded)
                return CreatedAtAction(nameof(RegisterUser), new { userName = userForRegistration.UserName }, new { Message = "User registration successful" });

            var errors = result.Errors.Select(e => e.Description);
            return BadRequest(errors);
        }
    }
}
