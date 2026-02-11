using Microsoft.AspNetCore.Identity;
using PriceNegotiationApp.Application;
using PriceNegotiationApp.Infrastructure.Identities;

namespace PriceNegotiationApp.Infrastructure.Data.Initializers
{
    public class MainInitializer
    {
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public MainInitializer(RoleManager<IdentityRole<Guid>> roleManager, UserManager<ApplicationUser> userManager)
        {
            _roleManager = roleManager;
            _userManager = userManager;
        }

        public async Task InitializeRolesAsync()
        {
            // Create roles if they don't exist
            if (!await _roleManager.RoleExistsAsync(Roles.Role_Customer))
                await _roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Role_Customer));

            if (!await _roleManager.RoleExistsAsync(Roles.Role_Staff))
                await _roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Role_Staff));

            if (!await _roleManager.RoleExistsAsync(Roles.Role_Admin))
                await _roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Role_Admin));
        }

        public async Task InitializeAdminUserAsync()
        {
            // Create admin user if it doesn't exist
            const string AdminMail = @"admin@app.com";
            var userWithMail = await _userManager.FindByEmailAsync(AdminMail);
            if (userWithMail == null)
            {
                var adminUser = new ApplicationUser("Admin", "Street", "City", "State", "00-000")
                {
                    UserName = "admin",
                    Email = AdminMail,
                    PhoneNumber = "123456789",
                };

                await _userManager.CreateAsync(adminUser, "Admin123!");

                var user = await _userManager.FindByEmailAsync(AdminMail);

                if (user != null)
                {
                    await _userManager.AddToRoleAsync(user, Roles.Role_Admin);
                }
            }
        }

        public async Task InitializeStaffUserAsync()
        {
            // Create admin user if it doesn't exist
            const string StaffMail = @"Staff1@app.com";
            if (_userManager.FindByEmailAsync(StaffMail).GetAwaiter().GetResult() == null)
            {
                var staffUser = new ApplicationUser("Bob Smith", "Street", "City", "State", "00-000")
                {
                    UserName = "Staff1",
                    Email = StaffMail,
                    PhoneNumber = "987654321",
                };

                await _userManager.CreateAsync(staffUser, "Staff123!");

                var user = await _userManager.FindByEmailAsync(StaffMail);

                if (user != null)
                    await _userManager.AddToRoleAsync(user, Roles.Role_Staff);
            }
        }
    }
}
