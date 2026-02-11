using Microsoft.AspNetCore.Identity;

namespace PriceNegotiationApp.Infrastructure.Identities
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string Name { get; set; }
        public string? StreetAddress { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string Role { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        /// <summary>
		/// Empty constructor for EF.
		/// </summary>
        private ApplicationUser() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public ApplicationUser(string name, string? streetAddress, string? city, string? state, string? postalCode)
        {
            Name = name;
            StreetAddress = streetAddress;
            City = city;
            State = state;
            PostalCode = postalCode;
            Role = "Customer";
        }
    }
}
