using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using PriceNegotiationApp.Api.Authorization;
using PriceNegotiationApp.Domain.Models.Negotiations;
using System.Security.Claims;

namespace PriceNegotiationApp.Api
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
            var userId = Guid.Parse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var isAdminOrStaff = user.IsInRole("Admin") || user.IsInRole("Staff");

            // Sprawdzamy, czy użytkownik jest właścicielem tego konkretnego zasobu
            var isResourceOwner = (resource.UserId == userId);

            // Przypadek 1: READ (Czytanie)
            if (requirement.Name == Operations.Read.Name)
            {
                // Czytać może Właściciel LUB Admin LUB Staff
                if (isResourceOwner || isAdminOrStaff)
                {
                    context.Succeed(requirement);
                }
            }

            // Przypadek 2: UPDATE / DELETE (Modyfikacja)
            else if (requirement.Name == Operations.Update.Name ||
                     requirement.Name == Operations.Delete.Name)
            {
                // Edytować/Usuwać może Właściciel LUB Admin (Staff zazwyczaj nie usuwa, ale jak chcesz to dodaj)
                if (isResourceOwner || user.IsInRole("Admin"))
                {
                    context.Succeed(requirement);
                }
            }

            else if (requirement.Name == Operations.ModifyNegotiationAsOwner.Name)
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