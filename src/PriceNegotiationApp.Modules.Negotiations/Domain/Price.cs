using Vogen;

namespace PriceNegotiationApp.Modules.Negotiations.Domain;

[ValueObject<decimal>(Conversions.None)]
public readonly partial record struct Price
{
    private static Validation Validate(decimal value) =>
        value > 0m ? Validation.Ok : Validation.Invalid("Price must be greater than zero.");
}



