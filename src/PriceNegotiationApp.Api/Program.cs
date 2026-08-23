using PriceNegotiationApp.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddApiServices();

var app = builder.Build();
app.UsePipeline();

await app.RunAsync();

public partial class Program;
