using NetArchTest.Rules;
using PriceNegotiationApp.Modules.Catalog;
using PriceNegotiationApp.Modules.Identity;
using PriceNegotiationApp.Modules.Negotiations;
using PriceNegotiationApp.SharedKernel;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.ArchitectureTests;

/// <summary>
/// Compiler-enforced boundaries are asserted here as executable architecture rules:
/// the composition root is the only cross-module edge, the kernel depends on nobody,
/// and domain namespaces stay persistence-free.
/// </summary>
public class ArchitectureShould
{
    private const string Api = "PriceNegotiationApp.Api";
    private const string Kernel = "PriceNegotiationApp.SharedKernel";
    private const string Catalog = "PriceNegotiationApp.Modules.Catalog";
    private const string Identity = "PriceNegotiationApp.Modules.Identity";
    private const string Negotiations = "PriceNegotiationApp.Modules.Negotiations";

    [Fact]
    public void Catalog_module_never_references_other_modules_or_the_composition_root() =>
        AssertNoDependencies(typeof(CatalogModule), Catalog, Identity, Negotiations, Api);

    [Fact]
    public void Identity_module_never_references_other_modules_or_the_composition_root() =>
        AssertNoDependencies(typeof(IdentityModule), Catalog, Negotiations, Api);

    [Fact]
    public void Negotiations_module_never_references_other_modules_or_the_composition_root() =>
        AssertNoDependencies(typeof(NegotiationsModule), Catalog, Identity, Api);

    [Fact]
    public void Shared_kernel_depends_on_nothing_above_itself() =>
        AssertNoDependencies(typeof(ErrorCodes), Kernel, Catalog, Identity, Negotiations, Api);

    [Fact]
    public void Domain_namespaces_stay_free_of_persistence_concerns()
    {
        var efFailure = Types.InAssembly(typeof(NegotiationsModule).Assembly)
            .That().ResideInNamespace($"{Negotiations}.Domain")
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();
        efFailure.IsSuccessful.ShouldBeTrue(Describe(efFailure));

        var catalogFailure = Types.InAssembly(typeof(CatalogModule).Assembly)
            .That().ResideInNamespace($"{Catalog}.Domain")
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();
        catalogFailure.IsSuccessful.ShouldBeTrue(Describe(catalogFailure));
    }

    private static void AssertNoDependencies(Type anchorType, string subject, params string[] forbidden)
    {
        var result = Types.InAssembly(anchorType.Assembly)
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"{subject} must not depend on [{string.Join(", ", forbidden)}] but does: {Describe(result)}");
    }

    private static string Describe(NetArchTest.Rules.TestResult result)
    {
        var failing = result.FailingTypeNames;
        return failing is { Count: > 0 }
            ? string.Join("; ", failing.Take(10))
            : "no offending types reported";
    }
}
