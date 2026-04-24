using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace PriceNegotiationApp.IntegrationTests
{
    public static class TestIdentities
    {
        public static readonly string AdminPayload = Build(
            new Claim(ClaimTypes.NameIdentifier, "00000000-0000-0000-0000-000000000001"),
            new Claim(ClaimTypes.Role, "Admin"));

        public static readonly string UserPayload = Build(
            new Claim(ClaimTypes.NameIdentifier, "00000000-0000-0000-0000-000000000002"),
            new Claim(ClaimTypes.Role, "User"));

        public static readonly string StaffPayload = Build(
            new Claim(ClaimTypes.NameIdentifier, "00000000-0000-0000-0000-000000000003"),
            new Claim(ClaimTypes.Role, "Staff"));

        public static string BuildDynamic(Guid userId, string role) =>
            Build(new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role));

        private static string Build(params Claim[] claims) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(claims.Select(c => new { c.Type, c.Value }))));
    }
}
