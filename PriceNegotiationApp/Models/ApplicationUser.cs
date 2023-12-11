using Microsoft.AspNetCore.Identity;
using PriceNegotiationApp.Models.DTO;
using System.ComponentModel.DataAnnotations;

namespace PriceNegotiationApp.Models
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


		// constructor to convert RegisterUserDTO to ApplicationUser
		public ApplicationUser(RegisterUserDTO userDto)
		{
			UserName = userDto.UserName;
			Name = userDto.Name;
			Email = userDto.Email;
			StreetAddress = userDto.StreetAddress;
			City = userDto.City;
			State = userDto.State;
			PostalCode = userDto.PostalCode;
			Role = userDto.Role;
		}
	}
}
