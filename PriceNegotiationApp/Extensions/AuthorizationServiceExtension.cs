using PriceNegotiationApp.Auth.Authorization.Resource_based;

namespace PriceNegotiationApp.Extensions
{
    public static class AuthorizationServiceExtension
    {
        internal static void AddAuthorizationWithPolicies(this IServiceCollection services)
        {
            services.AddAuthorization(opt =>
            {
                opt.AddPolicy(OperationRequirements.IsAdminOrStaffOrOwnerRequirement.Name, policy =>
                    policy.Requirements.Add(OperationRequirements.IsAdminOrStaffOrOwnerRequirement));
                opt.AddPolicy(OperationRequirements.IsOwnerRequirement.Name, policy =>
                    policy.Requirements.Add(OperationRequirements.IsOwnerRequirement));

                opt.AddPolicy(OperationRequirements.CreateRequirement.Name, policy =>
                    policy.Requirements.Add(OperationRequirements.CreateRequirement));
                opt.AddPolicy(OperationRequirements.ReadRequirement.Name, policy =>
                    policy.Requirements.Add(OperationRequirements.ReadRequirement));
                //policy.RequireAssertion(context =>
                //	context.User.IsInRole("Admin") ||
                //	context.User.IsInRole("Staff") ||
                //	(context.User.IsInRole("Customer") && context.User.));

                opt.AddPolicy(OperationRequirements.UpdateRequirement.Name, policy =>
                    policy.Requirements.Add(OperationRequirements.UpdateRequirement));
                opt.AddPolicy(OperationRequirements.DeleteRequirement.Name, policy =>
                    policy.Requirements.Add(OperationRequirements.DeleteRequirement));
            });
        }
    }
}
