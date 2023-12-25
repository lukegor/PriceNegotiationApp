using Microsoft.AspNetCore.Http;
using Moq;
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

            var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            httpContextAccessorMock.Setup(x => x.HttpContext.User).Returns(user);

            var claimsProvider = new HttpContextClaimsProvider(httpContextAccessorMock.Object);

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
