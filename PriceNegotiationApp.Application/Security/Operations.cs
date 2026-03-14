using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace PriceNegotiationApp.Api.Authorization
{
    public static class Operations
    {
        public static readonly OperationAuthorizationRequirement Create =
                new OperationAuthorizationRequirement() { Name = nameof(Create) };

        public static readonly OperationAuthorizationRequirement Read =
            new OperationAuthorizationRequirement { Name = nameof(Read) };

        public static readonly OperationAuthorizationRequirement Delete =
            new OperationAuthorizationRequirement() { Name = nameof(Delete) };

        // Procesy biznesowe
        public static readonly OperationAuthorizationRequirement ProposePrice = new() { Name = nameof(ProposePrice) };
        public static readonly OperationAuthorizationRequirement Close = new() { Name = nameof(Close) };
    }
}
