using Mono.TextTemplating;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Models.DTO;
using System.Xml.Linq;

namespace PriceNegotiationApp.Extensions.Conversions
{
	public static class RegisterUserDTOConverter
	{
		public static ApplicationUser ToDb(this RegisterUserDTO registerUser)
		{
			return new ApplicationUser
			{
				UserName = registerUser.UserName,
				Name = registerUser.Name,
				Email = registerUser.Email,
				StreetAddress = registerUser.StreetAddress,
				City = registerUser.City,
				State = registerUser.State,
				PostalCode = registerUser.PostalCode
			};
		}
	}
}