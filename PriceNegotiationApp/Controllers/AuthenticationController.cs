using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PriceNegotiationApp.Auth;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Models.DTO;
using PriceNegotiationApp.Services;
using PriceNegotiationApp.Utility;
using System.IdentityModel.Tokens.Jwt;

namespace PriceNegotiationApp.Controllers
{
	public class AuthenticationController : ControllerBase
	{
		private readonly AuthService _authService;

		public AuthenticationController(AuthService authService)
		{
			_authService = authService;
		}

		/// <summary>Log into an account</summary>
		/// <param name="username">Admin: admin Doctor: doctor Customer: customer</param>
		/// <param name="password">Admin: Asd123! Doctor: Doc123! Customer: Customer123!</param>
		/// <returns>Returns true if login is successful, false otherwise.</returns>
		[HttpPost("Login")]
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
		/// <returns>Returns a 201 Created response if successful; otherwise, returns a 400 Bad Request response
		/// with details of the validation errors or registration failure.
		/// </returns>
		[HttpPost("Registration")]
		[ProducesResponseType(StatusCodes.Status201Created)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		public async Task<IActionResult> RegisterUser([FromBody] RegisterUserDTO userForRegistration)
		{
			if (userForRegistration == null || !ModelState.IsValid)
				return BadRequest("Invalid user registration data");

			var result = await _authService.RegisterUserAsync(userForRegistration);

			if (result.Succeeded)
				return StatusCode(201);

			var errors = result.Errors.Select(e => e.Description);
			return BadRequest(errors);
		}

		public class LoginModel
		{
			public string Username { get; set; }
			public string Password { get; set; }
		}
	}
}
