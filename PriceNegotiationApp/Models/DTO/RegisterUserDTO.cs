using System.ComponentModel.DataAnnotations;

namespace PriceNegotiationApp.Models.DTO
{
	public class RegisterUserDTO
	{
		[Required]
		public string UserName { get; set; }
		[Required]
		public string Name { get; set; }
		[Required]
		[RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Invalid email address.")]
		public string Email { get; set; }
		public string? StreetAddress { get; set; }
		public string? City { get; set; }
		public string? State { get; set; }
		public string? PostalCode { get; set; }
		[Required]
		public string? Password { get; set; }
		[Required]
		[Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
		public string? ConfirmPassword { get; set; }
	}
}
