using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using PriceNegotiationApp.Application;
using PriceNegotiationApp.Application.Security;
using PriceNegotiationApp.Domain.Models.Negotiations;
using System.Security.Claims;

namespace PriceNegotiationApp.Api.Authorization
{
    public class NegotiationAuthorizationHandler
        : AuthorizationHandler<OperationAuthorizationRequirement, Negotiation>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            OperationAuthorizationRequirement requirement,
            Negotiation resource)
        {
            // Pobieramy rolę i ID użytkownika raz
            var user = context.User;
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Task.CompletedTask; // Nie udało się zidentyfikować użytkownika -> brak sukcesu
            }

            var isAdmin = user.IsInRole(Roles.Admin);
            var isStaff = user.IsInRole(Roles.Staff);
            var isAdminOrStaff = isAdmin || isStaff;

            // Sprawdzamy, czy użytkownik jest właścicielem tego konkretnego zasobu
            var isResourceOwner = resource.UserId == userId;

            // Przypadek 1: READ (Czytanie)
            if (requirement == Operations.Read)
            {
                // Czytać może Właściciel LUB Admin LUB Staff
                if (isResourceOwner || isAdminOrStaff)
                {
                    context.Succeed(requirement);
                }
            }
            else if (requirement == Operations.Delete)
            {
                // Usuwać może Właściciel LUB Admin
                if (isResourceOwner || isAdmin)
                {
                    context.Succeed(requirement);
                }
            }
            // Procesy biznesowe - tylko Owner
            else if (requirement == Operations.ProposePrice
                || requirement == Operations.Close)
            {
                if (isResourceOwner)
                {
                    context.Succeed(requirement);
                }
            }

            return Task.CompletedTask;
        }
    }
}
