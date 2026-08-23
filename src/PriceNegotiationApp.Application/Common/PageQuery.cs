namespace PriceNegotiationApp.Application.Common;

public sealed record PageQuery(int Page, int PageSize)
{
    public int SafePage => Math.Max(1, Page);

    public int SafePageSize => Math.Clamp(PageSize, 1, 100);

    public int Skip => (SafePage - 1) * SafePageSize;
}
