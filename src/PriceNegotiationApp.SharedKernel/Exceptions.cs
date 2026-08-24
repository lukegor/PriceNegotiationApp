namespace PriceNegotiationApp.SharedKernel;

public sealed class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class NotFoundException(string entityName, object key)
    : Exception($"{entityName} '{key}' was not found.")
{
    public string Code { get; } = $"{entityName.ToLowerInvariant().Replace(" ", string.Empty)}_not_found";
}

/// <summary>Request payload rejected before any business state was touched.</summary>
public sealed class InvalidRequestException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class UnauthorizedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ForbiddenAccessException() : Exception("Access to the requested resource is forbidden.");

public class DomainException(string message) : Exception(message);
