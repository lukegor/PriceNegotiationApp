namespace PriceNegotiationApp.SharedKernel;

public static class ErrorCodes
{
    public const string Forbidden = "forbidden";

    public const string ConcurrencyConflict = "concurrency_conflict";

    public const string ValidationFailed = "validation_failed";

    public const string DomainRuleViolated = "domain_rule_violated";

    public const string InternalError = "internal_error";
}
