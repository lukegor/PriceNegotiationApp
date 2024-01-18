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
		[DataType(DataType.EmailAddress)]
		[EmailAddress(ErrorMessage = "Invalid email address.")]
        public string Email { get; set; }
		public string? StreetAddress { get; set; }
		public string? City { get; set; }
		public string? State { get; set; }
        [DataType(DataType.PostalCode)]
        public string? PostalCode { get; set; }
		[Required]
		[DataType(DataType.Password)]
		public string? Password { get; set; }
		[Required]
		[Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
		public string? ConfirmPassword { get; set; }
	}
}
