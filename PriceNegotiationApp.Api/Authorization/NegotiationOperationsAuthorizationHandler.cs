using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using PriceNegotiationApp.Application;
using PriceNegotiationApp.Application.Security;
using PriceNegotiationApp.Domain.Models.Negotiations;
using System.Reflection;
using System.Security.Claims;

namespace PriceNegotiationApp.Api.Authorization
{
    public class NegotiationOperationsAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, Negotiation>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            OperationAuthorizationRequirement requirement,
            Negotiation negotiation)
        {
            // using reflection to get all roles from Roles
            var roles = typeof(Roles)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string))
                .Select(f => (string)f.GetValue(null))
                .ToArray();

            switch (requirement.Name)
            {
                case PolicyNames.Create:
                    if (roles.Any(role => context.User.HasClaim(ClaimTypes.Role, role)))
                    {
                        context.Succeed(requirement);
                    }
                    break;
                case PolicyNames.Read:
                    if (context.User.HasClaim(ClaimTypes.Role, Roles.Role_Customer) && Guid.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value) == negotiation.UserId
                        || context.User.HasClaim(ClaimTypes.Role, Roles.Role_Staff)
                        || context.User.HasClaim(ClaimTypes.Role, Roles.Role_Admin))
                    {
                        context.Succeed(requirement);
                    }
                    break;
                case PolicyNames.Update:
                    if (context.User.HasClaim(ClaimTypes.Role, Roles.Role_Customer) && Guid.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value) == negotiation.UserId
                        || context.User.HasClaim(ClaimTypes.Role, Roles.Role_Staff)
                        || context.User.HasClaim(ClaimTypes.Role, Roles.Role_Admin))
                    {
                        context.Succeed(requirement);
                    }
                    break;
                case PolicyNames.Delete:
                    if (context.User.HasClaim(ClaimTypes.Role, Roles.Role_Admin))
                    {
                        context.Succeed(requirement);
                    }
                    break;
                case PolicyNames.AdminOrStaffOrOwner:
                    if ((context.User.HasClaim(ClaimTypes.Role, Roles.Role_Customer) && Guid.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value) == negotiation.UserId)
                        || context.User.HasClaim(ClaimTypes.Role, Roles.Role_Staff)
                        || context.User.HasClaim(ClaimTypes.Role, Roles.Role_Admin))
                    {
                        context.Succeed(requirement);
                    }
                    break;
                case PolicyNames.Owner:
                    if (context.User.HasClaim(ClaimTypes.Role, Roles.Role_Customer) && Guid.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value) == negotiation.UserId)
                    {
                        context.Succeed(requirement);
                    }
                    break;
            }
            return Task.CompletedTask;
        }

    }
}
