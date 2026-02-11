using System.ComponentModel.DataAnnotations;

namespace PriceNegotiationApp.Application.Common.Identities.Dtos.Requests.RegisterUser
{
    public class RegisterUserRequestDto
    {
        public required string UserName { get; init; }
        public required string Name { get; init; }
        [DataType(DataType.EmailAddress)]
        public required string Email { get; init; }
        public string? StreetAddress { get; init; }
        public string? City { get; init; }
        public string? State { get; init; }
        [DataType(DataType.PostalCode)]
        public string? PostalCode { get; init; }
        [DataType(DataType.Password)]
        public required string Password { get; init; }
        public required string ConfirmPassword { get; init; }
    }
}
