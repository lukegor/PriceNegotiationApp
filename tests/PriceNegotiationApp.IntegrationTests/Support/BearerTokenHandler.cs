namespace PriceNegotiationApp.IntegrationTests.Support;

public sealed class TokenHolder
{
    public string? Token { get; set; }
}

public sealed class BearerTokenHandler(TokenHolder holder) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (holder.Token is { } token)
        {
            request.Headers.Authorization = new("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
