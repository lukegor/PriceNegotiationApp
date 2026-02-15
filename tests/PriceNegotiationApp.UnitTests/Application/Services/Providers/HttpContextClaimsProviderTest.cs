using Microsoft.AspNetCore.Http;
using NSubstitute;
using PriceNegotiationApp.Api.Providers;
using System.Security.Claims;

namespace PriceNegotiationApp.UnitTests.Application.Services.Providers
{
    public class HttpContextClaimsProviderTest
    {
        [Fact]
        public void HttpContextClaimsProvider_UserClaimsPrincipal_ReturnsExpectedClaimsPrincipal()
        {
            var userNameId = Guid.CreateVersion7();
            var userRole = "Admin";

            // Arrange
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userNameId.ToString()),
                new Claim(ClaimTypes.Role, userRole),
            };

            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthenticationType"));

            var context = new DefaultHttpContext();
            context.User = user;

            var accessorMock = Substitute.For<IHttpContextAccessor>();
            accessorMock.HttpContext.Returns(context);

            var claimsProvider = new HttpExecutionContext(accessorMock);

            // Act
            var userId = claimsProvider.UserId;
            var role = claimsProvider.Role;

            // Assert
            Assert.Equal(userNameId, userId);
            Assert.Equal(userRole, role);
        }
    }
}
