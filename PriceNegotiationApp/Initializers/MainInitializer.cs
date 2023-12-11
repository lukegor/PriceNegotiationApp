using Microsoft.AspNetCore.Identity;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Utility;

namespace PriceNegotiationApp.Initializers
{
	public class MainInitializer
	{
		private readonly RoleManager<IdentityRole> _roleManager;
		private readonly UserManager<IdentityUser> _userManager;

		public MainInitializer(RoleManager<IdentityRole> roleManager, UserManager<IdentityUser> userManager)
		{
			_roleManager = roleManager;
			_userManager = userManager;
		}

		public async Task InitializeRolesAsync()
		{
			// Create roles if they don't exist
			if (!await _roleManager.RoleExistsAsync(Roles.Role_Customer))
				await _roleManager.CreateAsync(new IdentityRole(Roles.Role_Customer));

			if (!await _roleManager.RoleExistsAsync(Roles.Role_Admin))
				await _roleManager.CreateAsync(new IdentityRole(Roles.Role_Admin));
		}

		public async Task InitializeAdminUserAsync()
		{
			// Create admin user if it doesn't exist
			if (_userManager.FindByEmailAsync("admin@admin.com").GetAwaiter().GetResult() == null)
			{
				var adminUser = new ApplicationUser
				{
					UserName = "admin",
					Email = "admin@admin.com",
					Name = "Admin",
					PhoneNumber = "1234567890",
					StreetAddress = "Street test",
					State = "SomeState",
					PostalCode = "12-345",
					City = "SomeCity"
				};

				await _userManager.CreateAsync(adminUser, "Asd123!");

				ApplicationUser user = (ApplicationUser)await _userManager.FindByEmailAsync("admin@admin.com");

				if (user != null)
					await _userManager.AddToRoleAsync(user, Roles.Role_Admin);
			}
		}
	}
}
