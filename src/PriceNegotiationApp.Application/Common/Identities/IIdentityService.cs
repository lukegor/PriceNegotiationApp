namespace PriceNegotiationApp.Application.Common.Identities
{
    public interface IIdentityService
    {
        Task<(bool Success, Guid UserId, string[] Errors)> CreateUserAsync(
            string email,
            string password,
            string name,
            string? street,
            string? city,
            string? state,
            string? postalCode);
    }
}
