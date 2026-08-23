namespace PriceNegotiationApp.Application.Exceptions;

public sealed class NotFoundException(string entityName, object key)
    : Exception($"{entityName} '{key}' was not found.")
{
    public string Code { get; } = $"{entityName.ToLowerInvariant().Replace(" ", string.Empty)}_not_found";
}
