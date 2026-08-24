namespace PriceNegotiationApp.BuildingBlocks;

/// <summary>Role-name contract shared by host authorization policies and module endpoint gates.</summary>
public static class UserRoles
{
    public const string Admin = "Admin";

    public const string Staff = "Staff";

    public const string Customer = "Customer";
}
