namespace PriceNegotiationApp.Application.Exceptions;

public sealed class ForbiddenAccessException() : Exception("Access to the requested resource is forbidden.");
