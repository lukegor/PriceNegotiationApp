using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Api.Endpoints.Negotiations.Accept;
using PriceNegotiationApp.Api.Endpoints.Negotiations.CounterPropose;
using PriceNegotiationApp.Api.Endpoints.Negotiations.Create;
using PriceNegotiationApp.Api.Endpoints.Negotiations.Get;
using PriceNegotiationApp.Api.Endpoints.Negotiations.List;
using PriceNegotiationApp.Api.Endpoints.Negotiations.ListMine;
using PriceNegotiationApp.Api.Endpoints.Negotiations.RejectCurrentOffer;
using PriceNegotiationApp.Api.Endpoints.Negotiations.Withdraw;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations;

public static class NegotiationEndpoints
{
    public static IEndpointRouteBuilder MapNegotiationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/negotiations")
            .WithTags("Negotiations")
            .RequireAuthorization();
        group.MapCreate();
        group.MapListMine();
        group.MapList();
        group.MapGetOne();
        group.MapCounterPropose();
        group.MapAccept();
        group.MapRejectCurrentOffer();
        group.MapWithdraw();
        return app;
    }
}
