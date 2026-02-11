using Microsoft.AspNetCore.Identity;
using PriceNegotiationApp.Application.Common.Identities;

namespace PriceNegotiationApp.Infrastructure.Identities
{
    public class IdentityService : IIdentityService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public IdentityService(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<(bool Success, Guid UserId, string[] Errors)> CreateUserAsync(
            string email,
            string password,
            string name,
            string? street,
            string? city,
            string? state,
            string? postalCode)
        {
            var user = new ApplicationUser(name, street, city, state, postalCode)
            {
                UserName = email,
                Email = email,
            };

            var result = await _userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                return (false, Guid.Empty, result.Errors.Select(e => e.Description).ToArray());
            }

            return (true, user.Id, Array.Empty<string>());
        }
    }
}
