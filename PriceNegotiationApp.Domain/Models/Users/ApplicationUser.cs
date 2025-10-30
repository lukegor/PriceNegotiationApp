using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace PriceNegotiationApp.Domain.Models.Users
{
	public class ApplicationUser: IdentityUser
	{
		[Required]
		public string Name { get; set; }
		public string? StreetAddress { get; set; }
		public string? City { get; set; }
		public string? State { get; set; }
		public string? PostalCode { get; set; }
		public string Role { get; set; }


		public ApplicationUser()
		{
			Name = string.Empty;
			Role = "Customer";
		}
	}
}
