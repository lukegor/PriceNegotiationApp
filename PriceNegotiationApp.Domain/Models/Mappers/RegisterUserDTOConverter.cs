using PriceNegotiationApp.Domain.Models.Dto.Requests;
using PriceNegotiationApp.Domain.Models.Users;
using System.Xml.Linq;

namespace PriceNegotiationApp.Domain.Models.Mappers
{
	public static class RegisterUserDTOConverter
	{
		public static ApplicationUser ToDb(this RegisterUserRequestDto registerUser)
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