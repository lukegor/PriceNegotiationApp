using Vogen;

namespace PriceNegotiationApp.Modules.Catalog.Domain;

[ValueObject<decimal>(Conversions.None)]
internal readonly partial record struct Price
{
    private static Validation Validate(decimal value) =>
        value > 0m ? Validation.Ok : Validation.Invalid("Price must be greater than zero.");
}
