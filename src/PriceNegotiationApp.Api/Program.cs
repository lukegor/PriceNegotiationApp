var app = WebApplication.Create(args);

app.MapGet("/", () => Results.Ok("PriceNegotiationApp"));

await app.RunAsync();
