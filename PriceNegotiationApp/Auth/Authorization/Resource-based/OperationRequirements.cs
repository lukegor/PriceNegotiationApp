using Microsoft.AspNetCore.Authorization.Infrastructure;
using PriceNegotiationApp.Utility;

namespace PriceNegotiationApp.Auth.Authorization.Resource_based
{
    public static class OperationRequirements
    {
        #region CRUD
        public static OperationAuthorizationRequirement CreateRequirement =
                new OperationAuthorizationRequirement() { Name = nameof(RequirementsNames.CreateRequirement) };
        public static OperationAuthorizationRequirement ReadRequirement =
            new OperationAuthorizationRequirement { Name = nameof(RequirementsNames.ReadRequirement) };

        public static OperationAuthorizationRequirement UpdateRequirement =
            new OperationAuthorizationRequirement() { Name = nameof(RequirementsNames.UpdateRequirement) };
        public static OperationAuthorizationRequirement DeleteRequirement =
            new OperationAuthorizationRequirement() { Name = nameof(RequirementsNames.DeleteRequirement) };
        #endregion
        public static OperationAuthorizationRequirement AdminOrStaffOrOwner =
            new OperationAuthorizationRequirement() { Name = "AdminOrStaffOrOwner" };
    }
}
