using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PriceNegotiationApp.Application.Negotiations;
using PriceNegotiationApp.Application.Negotiations.Requests.Commands;
using PriceNegotiationApp.Application.Negotiations.Requests.Queries;
using PriceNegotiationApp.Contracts.Negotiations.Dto.Requests;
using PriceNegotiationApp.Contracts.Negotiations.Dto.Response;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;
using PriceNegotiationApp.Presentation.Negotiations.Mappers;
using System.Globalization;

namespace PriceNegotiationApp.Api.Controllers
{
    [Area("Negotiations")]
    [Route("api/v1/[area]")]
    [ApiController]
    public class NegotiationController : ControllerBase
    {
        private readonly INegotiationService _service;

        public NegotiationController(INegotiationService service)
        {
            _service = service;
        }

        // GET: api/v1/Negotiations
        [HttpGet]
        [Route("all")]
        [ResponseCache(Duration = 5)] //Caches the HTTP response for 5 seconds
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Roles = "Admin, Staff")]
        [EndpointDescription("Retrieves a list of all negotiations.")]
        public async Task<ActionResult<IEnumerable<NegotiationResponseDto>>> GetNegotiations()
        {
            var negotiations = await _service.GetNegotiationsAsync();
            return Ok(negotiations.Select(x => x.ToResponseDto()));
        }

        // GET: api/v1/Negotiations/5
        [HttpGet("{id}")]
        [EndpointDescription("Retrieves a specific negotiation by its unique identifier.")]
        public async Task<Results<Ok<NegotiationResponseDto>, NotFound, ForbidHttpResult>> GetNegotiation(
            [FromRoute] NegotiationId id, CancellationToken cancellationToken)
        {
            var negotiation = await _service.GetNegotiationAsync(new GetNegotiationByIdQuery(id), cancellationToken);

            return TypedResults.Ok(negotiation.ToResponseDto());
        }

        // PUT: api/v1/Negotiation/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [NonAction]
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        [EndpointDescription("Updates a specific negotiation by its unique identifier.")]
        public async Task<Results<Ok<NegotiationResponseDto>, BadRequest, NotFound>> UpdateNegotiation(
            [FromRoute] NegotiationId id, [FromBody] UpdateNegotiationRequestDto request, CancellationToken cancellationToken)
        {
            var updateResult = await _service.UpdateNegotiationAsync(request.ToCommand(id), cancellationToken);

            return TypedResults.Ok(updateResult.ToResponseDto());
        }

        // POST: api/v1/Negotiation/5/reset-retries
        [HttpPost("{id}/reset-retries")]
        [Authorize(Roles = "Admin")]
        [EndpointDescription("Resets the retry count for the specified negotiation.")]
        public async Task<Results<Ok<NegotiationResponseDto>, BadRequest, NotFound>> AdminResetRetry(
            [FromRoute] NegotiationId id, CancellationToken cancellationToken)
        {
            var updateResult = await _service.ResetRetriesAsync(id, cancellationToken);
            return TypedResults.Ok(updateResult.ToResponseDto());
        }

        // PUT: api/v1/Negotiation/5/close
        [HttpPut("{id}/close")]
        [EndpointDescription("Closes negotiation if requested by the owner.")]
        public async Task<Results<Ok<NegotiationResponseDto>, NotFound, ForbidHttpResult>> CloseNegotiation(
            [FromRoute] NegotiationId id, CancellationToken cancellationToken)
        {
            var updateResult = await _service.CloseNegotiationAsync(id, cancellationToken);
            return TypedResults.Ok(updateResult.ToResponseDto());
        }

        // PATCH: api/v1/Negotiations/5/negotiate
        [HttpPatch("{negotiationId}/negotiate")]
        [EndpointDescription("Proposes a new price for a negotiation.")]
        public async Task<Results<Ok<string>, BadRequest<string>, NotFound, ForbidHttpResult>> ProposeNewPrice(
            [FromRoute] NegotiationId negotiationId, decimal proposedPrice, CancellationToken cancellationToken)
        {

            var response = await _service.ProposeNewPriceAsync(new ProposeNewPriceCommand(negotiationId, new ProposedPrice(proposedPrice)), cancellationToken);

            return response.Result switch
            {
                ProposePriceResult.Success => TypedResults.Ok("ProductPrice proposed successfully."),
                ProposePriceResult.Failed => TypedResults.BadRequest(string.Create(CultureInfo.InvariantCulture, $"Proposed price is too high. Max allowed price is {response.MaxAllowedPrice}.")),
            };
        }

        // POST: api/v1/Negotiations
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        [Authorize(Roles = "Customer")]
        [EndpointDescription("Creates a new negotiation.")]
        public async Task<Results<CreatedAtRoute<NegotiationResponseDto>, BadRequest>> CreateNegotiation(
            [FromBody] CreateNegotiationRequestDto requestDto, CancellationToken cancellationToken)
        {
            var negotiation = await _service.CreateNegotiationAsync(requestDto.ToCommand(), cancellationToken);

            return TypedResults.CreatedAtRoute(negotiation.ToResponseDto(), nameof(GetNegotiation), new { id = negotiation.NegotiationId });
        }

        // DELETE: api/v1/Negotiations/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        [EndpointDescription("Deletes a specific negotiation by its unique identifier.")]
        public async Task<Results<NoContent, NotFound>> DeleteNegotiation(
            [FromRoute] NegotiationId id, CancellationToken cancellationToken)
        {
            await _service.DeleteNegotiationAsync(id, cancellationToken);

            return TypedResults.NoContent();
        }
    }
}
