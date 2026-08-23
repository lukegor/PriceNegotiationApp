using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

using PriceNegotiationApp.Application;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Common.Identities;
using PriceNegotiationApp.Application.Common.Identities.Dtos;
using PriceNegotiationApp.Application.Common.Identities.Requests.Commands;
using PriceNegotiationApp.Application.Security;
using PriceNegotiationApp.Application.Services;
using PriceNegotiationApp.Domain.Models.Customers;

using System.Security.Claims;

namespace PriceNegotiationApp.Infrastructure.Identities
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IIdentityService _identityService;
        private readonly IJwtTokenGenerator _jwtHandler;
        private readonly CustomerFactory _customerFactory;
        private readonly ILogger<AuthService> _logger;
        private readonly IAppDbContext _dbContext;

        public AuthService(UserManager<ApplicationUser> userManager, IIdentityService identityService,
            IJwtTokenGenerator jwtHandler, CustomerFactory customerFactory, ILogger<AuthService> logger,
            IAppDbContext dbContext)
        {
            _userManager = userManager;
            _identityService = identityService;
            _jwtHandler = jwtHandler;
            _customerFactory = customerFactory;
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task<AuthResultDto> AuthenticateAsync(LoginCommand command)
        {
            var user = await _userManager.FindByNameAsync(command.Username);

            if (user == null || !await _userManager.CheckPasswordAsync(user, command.Password))
            {
                _logger.LogWarning("Authentication failed for username: {Username}", command.Username);
                return new AuthResultDto { ErrorMessage = "Invalid Authentication" };
            }

            var claims = await GetClaims(user);
            var token = await _jwtHandler.GenerateToken(claims);

            _logger.LogInformation("User {Username} authenticated successfully.", command.Username);

            return new AuthResultDto { IsAuthSuccessful = true, Token = token };
        }

        public async Task<IdentityResult> RegisterUserAsync(RegisterUserCommand command)
        {
            using var transaction = await _dbContext.BeginTransactionAsync();

            var user = new ApplicationUser(
                command.Name,
                command.StreetAddress,
                command.City,
                command.State,
                command.PostalCode)
            {
                UserName = command.UserName,
                Email = command.Email,
            };

            var result = await _userManager.CreateAsync(user, command.Password);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                throw new InvalidOperationException(string.Join(' ', errors));
            }

            await _userManager.AddToRoleAsync(user, Roles.Customer);
            _logger.LogInformation("User {UserName} registered successfully.", user.UserName);

            var customer = _customerFactory.Create(
                user.Id,
                command.Name
            );

            _dbContext.Customers.Add(customer);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return result;
        }

        private async Task<IReadOnlyCollection<Claim>> GetClaims(ApplicationUser user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            return claims;
        }
    }
}
