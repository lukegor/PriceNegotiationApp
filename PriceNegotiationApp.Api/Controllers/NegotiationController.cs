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

namespace PriceNegotiationApp.Api.Controllers
{
    [Area("Negotiations")]
    [Route("api/v1/[area]/[controller]")]
    [ApiController]
    public class NegotiationController(INegotiationService service) : ControllerBase
    {
        private readonly INegotiationService _service = service;

        /// <summary>
        /// Retrieves a list of all negotiations.
        /// </summary>
        /// <returns>Returns a 200 Ok response with a collection of negotiations</returns>
        // GET: api/Negotiations
        [HttpGet]
        [Route("all")]
        [ResponseCache(Duration = 5)] //Caches the HTTP response for 5 seconds
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Authorize(Roles = "Admin, Staff")]
        public async Task<ActionResult<IEnumerable<NegotiationResponseDto>>> GetNegotiations()
        {
            var negotiations = await _service.GetNegotiationsAsync();
            return Ok(negotiations.Select(x => x.ToResponseDto()));
        }

        /// <summary>
        /// Retrieves a specific negotiation by its unique identifier.
        /// </summary>
        /// <returns>Returns a negotiation with the specified ID if found</returns>
        // GET: api/Negotiations/5
        [HttpGet("{id}")]
        public async Task<Results<Ok<NegotiationResponseDto>, NotFound, ForbidHttpResult>> GetNegotiation(
            [FromRoute] NegotiationId id)
        {
            var negotiation = await _service.GetNegotiationAsync(new GetNegotiationByIdQuery(id));

            return TypedResults.Ok(negotiation.ToResponseDto());
        }

        /// <summary>
        /// Updates a specific negotiation by its unique identifier.
        /// </summary>
        /// <returns>Returns a 204 No Content response if the update is successful,</returns>
        // PUT: api/Negotiation/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        [Authorize(Roles = "Staff, Admin")]
        public async Task<Results<Ok<NegotiationResponseDto>, BadRequest, NotFound>> PutNegotiation(
            [FromRoute] NegotiationId id, [FromBody] UpdateNegotiationRequestDto request)
        {
            var updateResult = await _service.UpdateNegotiationAsync(request.ToCommand(id));

            return TypedResults.Ok(updateResult.ToResponseDto());
        }

        /// <summary>
        /// Closes negotiation if requested by owner.
        /// </summary>
        /// <returns>Returns a 200 Ok response if the update is successful.</returns>
        // PUT: api/Negotiation/5/close
        [HttpPut("{id}/close")]
        public async Task<Results<Ok<NegotiationResponseDto>, NotFound, ForbidHttpResult>> CloseNegotiation(
            [FromRoute] NegotiationId id)
        {
            var updateResult = await _service.CloseNegotiationAsync(id);
            return TypedResults.Ok(updateResult.ToResponseDto());
        }

        /// <summary>
        /// Proposes a new price for a negotiation.
        /// </summary>
        /// <returns></returns>
        // PATCH: api/Negotiations/5/negotiate
        [HttpPatch("{negotiationId}/negotiate")]
        public async Task<Results<Ok<string>, BadRequest<string>, NotFound, ForbidHttpResult>> ProposeNewPrice(
            [FromRoute] NegotiationId negotiationId, decimal proposedPrice)
        {

            var response = await _service.ProposeNewPriceAsync(new ProposeNewPriceCommand(negotiationId, new ProposedPrice(proposedPrice)));

            return response.Result switch
            {
                ProposePriceResult.Success => TypedResults.Ok("ProductPrice proposed successfully."),
                ProposePriceResult.Failed => TypedResults.BadRequest($"Proposed price is too high. Max allowed price is {response.MaxAllowedPrice}."),
            };
        }

        /// <summary>
        /// Creates a new negotiation.
        /// </summary>
        /// <returns>Returns a 201 Created response with the newly created negotiation</returns>
        // POST: api/Negotiations
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        [Authorize(Roles = "Customer")]
        public async Task<Results<CreatedAtRoute<NegotiationResponseDto>, BadRequest>> PostNegotiation(
            [FromBody] CreateNegotiationRequestDto requestDto)
        {
            var negotiation = await _service.CreateNegotiationAsync(requestDto.ToCommand());

            return TypedResults.CreatedAtRoute(negotiation.ToResponseDto(), nameof(GetNegotiation), new { id = negotiation.NegotiationId });
        }

        /// <summary>
        /// Deletes a specific negotiation by its unique identifier.
        /// </summary>
        /// <returns>a 204 No Content response if the deletion is successful.</returns>
        // DELETE: api/Negotiation/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<Results<NoContent, NotFound>> DeleteNegotiation([FromRoute] NegotiationId id)
        {
            await _service.DeleteNegotiationAsync(id);

            return TypedResults.NoContent();
        }
    }
}
