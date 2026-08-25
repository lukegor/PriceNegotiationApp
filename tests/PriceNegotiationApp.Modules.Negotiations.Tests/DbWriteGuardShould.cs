using Microsoft.EntityFrameworkCore;
using Npgsql;
using PriceNegotiationApp.SharedKernel;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.Modules.Negotiations.Tests;

public class DbWriteGuardShould
{
    private const string ConstraintName = "uq_negotiations_open_product_customer";

    [Fact]
    public void Detect_unique_violation_wrapped_in_DbUpdateException()
    {
        var inner = new PostgresException("duplicate key value", "ERROR", "ERROR",
            PostgresErrorCodes.UniqueViolation, constraintName: ConstraintName);

        var found = DbWriteGuard.IsUniqueViolation(new DbUpdateException("save failed", inner),
            out var constraint);

        found.ShouldBeTrue();
        constraint.ShouldBe(ConstraintName);
    }

    [Fact]
    public void Ignore_other_postgres_error_codes()
    {
        var inner = new PostgresException("foreign key violation", "ERROR", "ERROR",
            PostgresErrorCodes.ForeignKeyViolation, constraintName: ConstraintName);

        DbWriteGuard.IsUniqueViolation(new DbUpdateException("save failed", inner), out _)
            .ShouldBeFalse();
    }

    [Fact]
    public void Ignore_unrelated_exception_types()
    {
        DbWriteGuard.IsUniqueViolation(new InvalidOperationException("nope"), out _)
            .ShouldBeFalse();
    }

    [Fact]
    public void SaveOrConflict_throws_factory_exception_with_constraint_name()
    {
        var db = new ThrowingDbContext(withUniqueViolation: true);

        var thrown = Should.Throw<ConflictException>(() =>
            db.SaveOrConflictAsync(
                constraint => new ConflictException($"hit:{constraint}", "conflict"),
                TestContext.Current.CancellationToken).GetAwaiter().GetResult());

        thrown.Code.ShouldBe($"hit:{ConstraintName}");
    }

    [Fact]
    public void SaveOrConflict_rerethrows_non_unique_failures()
    {
        var db = new ThrowingDbContext(withUniqueViolation: false);

        var thrown = Should.Throw<DbUpdateException>(() =>
            db.SaveOrConflictAsync(
                constraint => new ConflictException(constraint, "conflict"),
                TestContext.Current.CancellationToken).GetAwaiter().GetResult());

        thrown.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    private sealed class ThrowingDbContext(bool withUniqueViolation = true) : DbContext
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Exception inner = withUniqueViolation
                ? new PostgresException("duplicate key value", "ERROR", "ERROR",
                    PostgresErrorCodes.UniqueViolation, constraintName: ConstraintName)
                : new InvalidOperationException("boom");
            throw new DbUpdateException("save failed", inner);
        }
    }
}
