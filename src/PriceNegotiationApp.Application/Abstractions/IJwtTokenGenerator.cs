namespace PriceNegotiationApp.Application.Abstractions;

public interface IJwtTokenGenerator
{
    Task<(string Token, DateTimeOffset ExpiresAtUtc)> GenerateAsync(Guid userId, string email, IReadOnlyCollection<string> roles);
}
