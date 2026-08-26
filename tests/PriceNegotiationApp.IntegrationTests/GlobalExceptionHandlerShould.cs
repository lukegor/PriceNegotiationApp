using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PriceNegotiationApp.Api;
using Shouldly;
using System.Text.Json;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

// Plain unit facts over the exception mapper; no Docker container required.
public class GlobalExceptionHandlerShould
{
    [Fact]
    public async Task Map_concurrency_conflicts_to_409_with_stable_code()
    {
        var (status, code) = await HandleAsync(new DbUpdateConcurrencyException("xmin race"));

        status.ShouldBe(StatusCodes.Status409Conflict);
        code.ShouldBe("concurrency_conflict");
    }

    [Fact]
    public async Task Keep_unknown_exceptions_on_the_internal_error_fallback()
    {
        var (status, code) = await HandleAsync(new InvalidOperationException("boom"));

        status.ShouldBe(StatusCodes.Status500InternalServerError);
        code.ShouldBe("internal_error");
    }

    private static async Task<(int Status, string Code)> HandleAsync(Exception exception)
    {
        var services = new ServiceCollection()
            .AddSingleton<IOptions<ProblemDetailsOptions>>(
                Options.Create(new ProblemDetailsOptions()))
            .AddSingleton<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>(
                Options.Create(new Microsoft.AspNetCore.Http.Json.JsonOptions()))
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        var sut = new GlobalExceptionHandler(
            services.GetRequiredService<IProblemDetailsService>(),
            new TestEnvironment(),
            NullLogger<GlobalExceptionHandler>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await sut.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        return (context.Response.StatusCode, document.RootElement.GetProperty("code").GetString()!);
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public string EnvironmentName { get; set; } = "Testing";
    }
}
