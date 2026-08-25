using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using PriceNegotiationApp.Api;
using PriceNegotiationApp.Modules.Catalog;
using PriceNegotiationApp.Modules.Identity;
using PriceNegotiationApp.Modules.Negotiations;
using PriceNegotiationApp.SharedKernel;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace PriceNegotiationApp.ArchitectureTests;

/// <summary>
/// Executable architecture rules: the composition root is the only cross-module edge,
/// the kernel depends on nobody, and domain namespaces stay persistence-free.
/// Each rule throws an ArchRuleException listing every violating type on failure.
/// </summary>
public class ArchitectureShould
{
    private const string Api = "PriceNegotiationApp.Api";
    private const string Kernel = "PriceNegotiationApp.SharedKernel";
    private const string Catalog = "PriceNegotiationApp.Modules.Catalog";
    private const string Identity = "PriceNegotiationApp.Modules.Identity";
    private const string Negotiations = "PriceNegotiationApp.Modules.Negotiations";

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(CatalogModule).Assembly,
            typeof(IdentityModule).Assembly,
            typeof(NegotiationsModule).Assembly,
            typeof(ErrorCodes).Assembly,
            typeof(GlobalExceptionHandler).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> KernelTypes =
        Types().That().ResideInAssembly(typeof(ErrorCodes).Assembly).As("shared kernel");

    private static readonly IObjectProvider<IType> CatalogTypes =
        Types().That().ResideInAssembly(typeof(CatalogModule).Assembly).As("catalog module");

    private static readonly IObjectProvider<IType> IdentityTypes =
        Types().That().ResideInAssembly(typeof(IdentityModule).Assembly).As("identity module");

    private static readonly IObjectProvider<IType> NegotiationsTypes =
        Types().That().ResideInAssembly(typeof(NegotiationsModule).Assembly).As("negotiations module");

    private static readonly IObjectProvider<IType> CompositionRoot =
        Types().That().ResideInAssembly(typeof(GlobalExceptionHandler).Assembly).As("composition root");

    private static readonly IObjectProvider<IType> EntityFramework =
        Types().That().ResideInNamespace(@"^Microsoft\.EntityFrameworkCore").As("EF Core");

    [Fact]
    public void Catalog_module_never_references_other_modules_or_the_composition_root() =>
        Types().That().Are(CatalogTypes)
            .Should().NotDependOnAny(AnyOf(IdentityTypes, NegotiationsTypes, CompositionRoot))
            .Check(Architecture);

    [Fact]
    public void Identity_module_never_references_other_modules_or_the_composition_root() =>
        Types().That().Are(IdentityTypes)
            .Should().NotDependOnAny(AnyOf(CatalogTypes, NegotiationsTypes, CompositionRoot))
            .Check(Architecture);

    [Fact]
    public void Negotiations_module_never_references_other_modules_or_the_composition_root() =>
        Types().That().Are(NegotiationsTypes)
            .Should().NotDependOnAny(AnyOf(CatalogTypes, IdentityTypes, CompositionRoot))
            .Check(Architecture);

    [Fact]
    public void Shared_kernel_depends_on_nothing_above_itself() =>
        Types().That().Are(KernelTypes)
            .Should().NotDependOnAny(AnyOf(CatalogTypes, IdentityTypes, NegotiationsTypes, CompositionRoot))
            .Check(Architecture);

    [Fact]
    public void Domain_namespaces_stay_free_of_persistence_concerns()
    {
        var catalogDomain = Types().That().ResideInNamespace($"{Catalog}.Domain").As("catalog domain");
        var negotiationsDomain = Types().That().ResideInNamespace($"{Negotiations}.Domain").As("negotiations domain");

        Types().That().Are(catalogDomain).Or().Are(negotiationsDomain)
            .Should().NotDependOnAny(EntityFramework)
            .Check(Architecture);
    }

    private static IObjectProvider<IType> AnyOf(params IObjectProvider<IType>[] sets)
    {
        var clause = Types().That().Are(sets[0]);
        for (var i = 1; i < sets.Length; i++)
        {
            clause = clause.Or().Are(sets[i]);
        }

        return clause.As(string.Join(" or ", sets.Select(s => s.Description)));
    }
}
