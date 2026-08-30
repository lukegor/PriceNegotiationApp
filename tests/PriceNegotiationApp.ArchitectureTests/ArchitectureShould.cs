using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using PriceNegotiationApp.Api;
using PriceNegotiationApp.Modules.Catalog.Contracts;
using PriceNegotiationApp.Modules.Catalog.Infrastructure;
using PriceNegotiationApp.Modules.Identity.Contracts;
using PriceNegotiationApp.Modules.Identity.Infrastructure;
using PriceNegotiationApp.Modules.Negotiations.Contracts;
using PriceNegotiationApp.Modules.Negotiations.Infrastructure;
using PriceNegotiationApp.SharedKernel;
using Shouldly;
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
            typeof(ProductSnapshot).Assembly,
            typeof(IdentityErrorCodes).Assembly,
            typeof(NegotiationErrorCodes).Assembly,
            typeof(ErrorCodes).Assembly,
            typeof(GlobalExceptionHandler).Assembly,
            typeof(CatalogModule).Assembly,
            typeof(IdentityModule).Assembly,
            typeof(NegotiationsModule).Assembly)
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
        Types().That().ResideInNamespace("Microsoft.EntityFrameworkCore").As("EF Core");

    private static readonly IObjectProvider<IType> CatalogPersistence =
        Types().That().ResideInNamespace($"{Catalog}.Infrastructure.Persistence").As("catalog persistence");

    private static readonly IObjectProvider<IType> NegotiationsPersistence =
        Types().That().ResideInNamespace($"{Negotiations}.Infrastructure.Persistence").As("negotiations persistence");

    private static readonly IObjectProvider<IType> PersistenceNamespaces =
        Types().That().ResideInNamespace($"{Catalog}.Infrastructure.Persistence")
            .Or().ResideInNamespace($"{Negotiations}.Infrastructure.Persistence")
            .As("persistence namespaces");

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
    public void Negotiations_module_depends_on_catalog_ports_only()
    {
        // Negotiations must not depend on Identity, CompositionRoot, or Catalog internals
        var forbidden = Types().That().Are(IdentityTypes).As("identity module")
            .Or().Are(CompositionRoot).As("composition root")
            .Or().ResideInNamespace($"{Catalog}.Infrastructure.Persistence").As("catalog persistence")
            .Or().ResideInNamespace($"{Catalog}.Infrastructure.Seeding").As("catalog seeding");

        Types().That().Are(NegotiationsTypes)
            .Should().NotDependOnAny(forbidden)
            .Check(Architecture);
    }

    [Fact]
    public void Shared_kernel_depends_on_nothing_above_itself() =>
        Types().That().Are(KernelTypes)
            .Should().NotDependOnAny(AnyOf(CatalogTypes, IdentityTypes, NegotiationsTypes, CompositionRoot))
            .Check(Architecture);

    [Fact]
    public void Catalog_infrastructure_persistence_isolation()
    {
        // Persistence types must not depend on EF Core directly (DbContext handles it)
        var catalogPersistenceTypes = Types().That().ResideInNamespace($"{Catalog}.Infrastructure.Persistence");
        catalogPersistenceTypes.Should().NotDependOnAny(EntityFramework).Check(Architecture);
    }

    [Fact]
    public void Negotiations_infrastructure_persistence_isolation()
    {
        var negotiationsPersistenceTypes = Types().That().ResideInNamespace($"{Negotiations}.Infrastructure.Persistence");
        negotiationsPersistenceTypes.Should().NotDependOnAny(EntityFramework).Check(Architecture);
    }

    [Fact]
    public void Port_contracts_stay_persistence_free()
    {
        var catalogPorts = Types().That().ResideInNamespace($"{Catalog}.Contracts").As("catalog ports");

        catalogPorts.Should().NotDependOnAny(PersistenceNamespaces).Check(Architecture);
    }

    [Fact]
    public void Repository_ceremony_stays_out_of_the_codebase()
    {
        // F-05 doctrine: module DbContext is the unit of work, DbSet<T> the aggregate
        // collection. A repository layer re-introduces ceremony without payoff here.
        var repositories = Types().That().HaveFullNameContaining("Repository");

        repositories.GetObjects(Architecture).ShouldBeEmpty(
            "repository-style types must not appear; use the module DbContext directly");
    }

    [Fact]
    public void Endpoint_mapping_types_stay_transport_only()
    {
        var endpoints = Types().That().HaveFullNameEndingWith("Endpoints").As("endpoint mapping types");

        endpoints.Should().NotDependOnAny(EntityFramework).Check(Architecture);
        endpoints.Should().NotDependOnAny(PersistenceNamespaces).Check(Architecture);
    }

    [Fact]
    public void Only_handlers_seeding_and_the_write_guard_commit_the_unit_of_work()
    {
        // F-05 doctrine, enforcement side: the single commit point lives in the
        // owning handler (or the seeding services / write guard / provisioning
        // helper inside the create-flow transaction). Nothing else may flush.
        var suffixAllowList = new[] { "Handler.cs", "SeedingHostedService.cs" };
        var nameAllowList = new[] { "DbWriteGuard.cs", "NegotiationAccess.cs" };

        var offenders = Directory
            .EnumerateFiles(Path.Combine(FindRepoRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && File.ReadAllText(path).Contains(".SaveChangesAsync(", StringComparison.Ordinal)
                           && !suffixAllowList.Any(path.EndsWith)
                           && !nameAllowList.Contains(Path.GetFileName(path)))
            .Select(path => Path.GetRelativePath(FindRepoRoot(), path))
            .ToList();

        offenders.ShouldBeEmpty(
            "SaveChangesAsync commits belong to feature handlers, seeding services, " +
            "DbWriteGuard, or NegotiationAccess provisioning");
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PriceNegotiationApp.slnx")))
        {
            directory = directory.Parent;
        }

        return directory!.FullName;
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
