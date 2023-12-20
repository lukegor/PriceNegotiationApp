using PriceNegotiationApp.Models;
using Microsoft.AspNetCore.Identity;

namespace PriceNegotiationApp.Extensions.Conversions
{
	public static class IdentityUserConverter
	{
		public static ApplicationUser ToDb(this IdentityUser identityUser)
		{
			return new ApplicationUser
			{
				Id = identityUser.Id,
				UserName = identityUser.UserName,
				NormalizedUserName = identityUser.NormalizedUserName,
				Email = identityUser.Email,
				NormalizedEmail = identityUser.NormalizedEmail,
				EmailConfirmed = identityUser.EmailConfirmed,
				PasswordHash = identityUser.PasswordHash,
				SecurityStamp = identityUser.SecurityStamp,
				ConcurrencyStamp = identityUser.ConcurrencyStamp,
				PhoneNumber = identityUser.PhoneNumber,
				PhoneNumberConfirmed = identityUser.PhoneNumberConfirmed,
				TwoFactorEnabled = identityUser.TwoFactorEnabled,
				LockoutEnd = identityUser.LockoutEnd,
				LockoutEnabled = identityUser.LockoutEnabled,
				AccessFailedCount = identityUser.AccessFailedCount,
				Name = string.Empty,
				StreetAddress = null,
				City = null,
				State = null,
				PostalCode = null
			};
		}
	}
}
