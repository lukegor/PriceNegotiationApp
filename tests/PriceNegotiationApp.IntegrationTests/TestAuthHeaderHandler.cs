using System.Net.Http.Headers;

namespace PriceNegotiationApp.IntegrationTests
{
    public class TestAuthHeaderHandler : DelegatingHandler
    {
        private readonly string _authPayload;

        public TestAuthHeaderHandler(string authPayload)
        {
            _authPayload = authPayload;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("TestScheme", _authPayload);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
