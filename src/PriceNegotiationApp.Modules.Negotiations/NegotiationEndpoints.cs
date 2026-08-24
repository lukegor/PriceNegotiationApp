using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Features;

namespace PriceNegotiationApp.Modules.Negotiations;

public static class NegotiationEndpoints
{
    public static IEndpointRouteBuilder MapNegotiationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/negotiations").WithTags("Negotiations");
        group.MapCreate();
        group.MapListMine();
        group.MapList();
        group.MapGetOne();
        group.MapCounterPropose();
        group.MapAccept();
        group.MapDecline();
        group.MapWithdraw();
        return app;
    }
}
