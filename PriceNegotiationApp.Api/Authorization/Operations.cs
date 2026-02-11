using Microsoft.AspNetCore.Authorization.Infrastructure;
using PriceNegotiationApp.Application.Security;

namespace PriceNegotiationApp.Api.Authorization
{
    public static class Operations
    {
        #region CRUD
        public static OperationAuthorizationRequirement Create =
                new OperationAuthorizationRequirement() { Name = nameof(PolicyNames.Create) };
        public static OperationAuthorizationRequirement Read =
            new OperationAuthorizationRequirement { Name = nameof(PolicyNames.Read) };

        public static OperationAuthorizationRequirement Update =
            new OperationAuthorizationRequirement() { Name = nameof(PolicyNames.Update) };
        public static OperationAuthorizationRequirement Delete =
            new OperationAuthorizationRequirement() { Name = nameof(PolicyNames.Delete) };
        #endregion

        public static OperationAuthorizationRequirement ModifyNegotiationAsOwner =
            new() { Name = nameof(PolicyNames.ModifyNegotiationAsOwner) };
    }
}
