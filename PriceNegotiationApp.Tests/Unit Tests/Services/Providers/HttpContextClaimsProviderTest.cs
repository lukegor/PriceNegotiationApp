using Microsoft.AspNetCore.Http;
using NSubstitute;
using PriceNegotiationApp.Services.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace PriceNegotiationApp.Tests.Unit_Tests.Services.Providers
{
    public class HttpContextClaimsProviderTest
    {
        [Fact]
        public void HttpContextClaimsProvider_UserClaimsPrincipal_ReturnsExpectedClaimsPrincipal()
        {
            // Arrange
            var claims = new[]
            {
            new Claim(ClaimTypes.Name, "TestUser"),
            new Claim(ClaimTypes.Role, "Admin"),
        };

            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthenticationType"));

			var httpContextAccessorSubstitute = Substitute.For<IHttpContextAccessor>();
			httpContextAccessorSubstitute.HttpContext.User.Returns(user);

			var claimsProvider = new HttpContextClaimsProvider(httpContextAccessorSubstitute);

            // Act
            var result = claimsProvider.UserClaimsPrincipal;

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Identity.IsAuthenticated);
            Assert.Equal("TestUser", result.Identity.Name);
            Assert.True(result.IsInRole("Admin"));
        }
    }
}
