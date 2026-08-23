namespace PriceNegotiationApp.Application.Exceptions;

/// <summary>Request payload rejected before any business state was touched. Maps to HTTP 400.</summary>
public sealed class InvalidRequestException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
