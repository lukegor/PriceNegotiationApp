using Microsoft.AspNetCore.Identity;
using PriceNegotiationApp.Extensions.Conversions;
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

			if (!await _roleManager.RoleExistsAsync(Roles.Role_Staff))
				await _roleManager.CreateAsync(new IdentityRole(Roles.Role_Staff));

			if (!await _roleManager.RoleExistsAsync(Roles.Role_Admin))
				await _roleManager.CreateAsync(new IdentityRole(Roles.Role_Admin));
		}

		public async Task InitializeAdminUserAsync()
		{
			// Create admin user if it doesn't exist
			const string AdminMail = @"admin@app.com";
			if (_userManager.FindByEmailAsync(AdminMail).GetAwaiter().GetResult() == null)
			{
				var adminUser = new ApplicationUser
				{
					UserName = "admin",
					Email = AdminMail,
					Name = "Admin",
					PhoneNumber = "123456789",
					StreetAddress = "Street",
					State = "State",
					PostalCode = "00-000",
					City = "City"
				};

				await _userManager.CreateAsync(adminUser, "Admin123!");

				ApplicationUser user = (ApplicationUser)await _userManager.FindByEmailAsync(AdminMail);

				if (user != null)
					await _userManager.AddToRoleAsync(user, Roles.Role_Admin);
			}
		}

		public async Task InitializeStaffUserAsync()
		{
			// Create admin user if it doesn't exist
			const string StaffMail = @"Staff1@app.com";
			if (_userManager.FindByEmailAsync(StaffMail).GetAwaiter().GetResult() == null)
			{
				var staffUser = new ApplicationUser
				{
					UserName = "Staff1",
					Email = StaffMail,
					Name = "Bob Smith",
					PhoneNumber = "987654321",
					StreetAddress = "Street",
					State = "State",
					PostalCode = "00-000",
					City = "City"
				};

				await _userManager.CreateAsync(staffUser, "Staff123!");

				ApplicationUser user = (ApplicationUser)await _userManager.FindByEmailAsync(StaffMail);

				if (user != null)
					await _userManager.AddToRoleAsync(user, Roles.Role_Staff);
			}
		}
	}
}
