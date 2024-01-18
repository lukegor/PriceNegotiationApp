using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PriceNegotiationApp.Auth;
using PriceNegotiationApp.Extensions;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Models.DTO;
using PriceNegotiationApp.Services;
using PriceNegotiationApp.Utility;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;

namespace PriceNegotiationApp.Controllers
{
	public class AuthenticationController : ControllerBase
	{
		private readonly IAuthService _authService;

		public AuthenticationController(IAuthService authService)
		{
			_authService = authService;
		}

		/// <summary>Log into an account</summary>
		/// <param name="model">username and password</param>
		/// <returns>Returns true if login is successful, false otherwise.</returns>
		[HttpPost("Login")]
		[AllowAnonymous]
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		public async Task<IActionResult> Login([FromBody] LoginModel model)
		{
			var authResponse = await _authService.AuthenticateAsync(model);

			if (!authResponse.IsAuthSuccessful)
				return Unauthorized(authResponse);

			return Ok(authResponse);
		}

		/// <summary>Log out of an account</summary>
		/// <returns>Returns a 200 Ok response </returns>
		[HttpPost("Logout")]
		[ProducesResponseType(StatusCodes.Status200OK)]
		public async Task<IActionResult> Logout()
		{
			await _authService.SignOutAsync();
			return Ok();
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
		public async Task<IActionResult> RegisterUser([FromBody] RegisterUserDTO userForRegistration)
		{
            var inputErrors = ModelStateHelper.GetErrors(ModelState);
            bool isEmailInUse = await _authService.IsEmailInUse(userForRegistration.Email);
			bool isUserNameInUse = await _authService.IsUsernameInUse(userForRegistration.UserName);

            if (userForRegistration == null || inputErrors.Any())
			{
				return BadRequest(inputErrors);
			}
            else if (isEmailInUse)
			{
				return BadRequest("The email is already in use");
			}
			else if (isUserNameInUse)
			{
                return BadRequest("The username is already in use");
            }

			var result = await _authService.RegisterUserAsync(userForRegistration);

			if (result.Succeeded)
                return CreatedAtAction(nameof(RegisterUser), new { userName = userForRegistration.UserName }, new { Message = "User registration successful" });

            var errors = result.Errors.Select(e => e.Description);
			return BadRequest(errors);
		}

        public class LoginModel
		{
			[Required]
			public string Username { get; set; }
			[Required]
			public string Password { get; set; }
		}
	}
}
