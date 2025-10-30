using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Reflection;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Utility.Utility;

namespace PriceNegotiationApp.Auth.Authorization.Resource_based
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
                case RequirementsNames.CreateRequirement:
                    if (roles.Any(role => context.User.HasClaim(ClaimTypes.Role, role)))
                    {
                        context.Succeed(requirement);
                    }
                    break;
                case RequirementsNames.ReadRequirement:
                    if (context.User.HasClaim(ClaimTypes.Role, Roles.Role_Customer) && context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value == negotiation.UserId
                        || context.User.HasClaim(ClaimTypes.Role, Roles.Role_Staff)
                        || context.User.HasClaim(ClaimTypes.Role, Roles.Role_Admin))
                    {
                        context.Succeed(requirement);
                    }
                    break;
                case RequirementsNames.UpdateRequirement:
                    if (context.User.HasClaim(ClaimTypes.Role, Roles.Role_Customer) && context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value == negotiation.UserId
                        || context.User.HasClaim(ClaimTypes.Role, Roles.Role_Staff)
                        || context.User.HasClaim(ClaimTypes.Role, Roles.Role_Admin))
                    {
                        context.Succeed(requirement);
                    }
                    break;
                case RequirementsNames.DeleteRequirement:
                    if (context.User.HasClaim(ClaimTypes.Role, Roles.Role_Admin))
                    {
                        context.Succeed(requirement);
                    }
                    break;
                case RequirementsNames.IsAdminOrStaffOrOwnerRequirement:
                    if ((context.User.HasClaim(ClaimTypes.Role, Roles.Role_Customer) && context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value == negotiation.UserId)
                        || context.User.HasClaim(ClaimTypes.Role, Roles.Role_Staff)
                        || context.User.HasClaim(ClaimTypes.Role, Roles.Role_Admin))
                    {
                        context.Succeed(requirement);
                    }
                    break;
                case RequirementsNames.IsOwnerRequirement:
                    if (context.User.HasClaim(ClaimTypes.Role, Roles.Role_Customer) && context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value == negotiation.UserId)
                    {
                        context.Succeed(requirement);
                    }
                    break;
            }
            return Task.CompletedTask;
        }

    }
}
