namespace PriceNegotiationApp.Application.Abstractions;

public sealed record RegistrationOutcome(
    bool Succeeded,
    Guid UserId,
    string? ErrorDescription,
    bool EmailAlreadyTaken)
{
    public static RegistrationOutcome Success(Guid userId) => new(true, userId, null, false);

    public static RegistrationOutcome DuplicateEmail() =>
        new(false, Guid.Empty, "Email already registered.", true);

    public static RegistrationOutcome ValidationFailed(string description) =>
        new(false, Guid.Empty, description, false);
}
