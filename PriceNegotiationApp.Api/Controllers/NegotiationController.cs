using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceNegotiationApp.Application.Negotiations.Dto.Requests.CreateNegotiation;
using PriceNegotiationApp.Application.Negotiations.Dto.Requests.UpdateNegotiation;
using PriceNegotiationApp.Application.Negotiations.Dto.Response;
using PriceNegotiationApp.Application.Services;
using PriceNegotiationApp.Domain.Models.Negotiations;

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
        /// <returns>
        /// Returns a 200 Ok response with a collection of negotiations
        /// </returns>
        // GET: api/Negotiations
        [HttpGet]
        [Route("all")]
        [ResponseCache(Duration = 5)] //Caches the HTTP response for 5 seconds
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [Authorize(Roles = "Admin, Staff")]
        public async Task<ActionResult<IEnumerable<Negotiation>>> GetNegotiations()
        {
            var negotiations = await _service.GetNegotiationsAsync();
            return Ok(negotiations);
        }

        /// <summary>
        /// Retrieves a specific negotiation by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the negotiation to retrieve.</param>
        /// <returns>
        /// Returns a negotiation with the specified ID if found
        /// </returns>
        // GET: api/Negotiations/5
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Negotiation>> GetNegotiation([FromRoute] NegotiationId id)
        {
            var negotiation = await _service.GetNegotiationAsync(id);

            return Ok(negotiation);
        }

        /// <summary>
        /// Updates a specific negotiation by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the negotiation to update.</param>
        /// <param name="negotiation">The updated negotiation data.</param>
        /// <returns>
        /// Returns a 204 No Content response if the update is successful,
        /// </returns>
        // PUT: api/Negotiation/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Roles = "Staff, Admin")]
        public async Task<IActionResult> PutNegotiation([FromRoute] NegotiationId id, [FromBody] UpdateNegotiationRequestDto negotiation)
        {
            var updateResult = await _service.UpdateNegotiationAsync(id, negotiation);

            return Ok(updateResult);
        }

        /// <summary>
        /// Closes negotiation if requested by owner
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        // PUT: api/Negotiation/5/close
        [HttpPut("{id}/close")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CloseNegotiation([FromRoute] NegotiationId id)
        {
            var updateResult = await _service.CloseNegotiationAsync(id);
            return Ok(updateResult);
        }

        /// <summary>
        /// Proposes a new price for a negotiation.
        /// </summary>
        /// <param name="negotiationId">The unique identifier of the negotiation to update.</param>
        /// <param name="proposedPrice">The proposed price for the negotiation.</param>
        // PATCH: api/Negotiations/5/negotiate
        [HttpPatch("{negotiationId}/negotiate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ProposeNewPrice([FromRoute] NegotiationId negotiationId, decimal proposedPrice)
        {

            var response = await _service.ProposeNewPriceAsync(negotiationId, proposedPrice);

            return response.Result switch
            {
                ProposePriceResultResponseDto.Success => Ok("ProductPrice proposed successfully."),
                ProposePriceResultResponseDto.Failed => BadRequest($"Proposed price is too high. Max allowed price is {response.MaxAllowedPrice}."),
            };
        }

        /// <summary>
        /// Creates a new negotiation.
        /// </summary>
        /// <param name="requestDto">The details of the negotiation to create.</param>
        /// <returns>
        /// Returns a 201 Created response with the newly created negotiation and a location header pointing to the negotiation,
        /// </returns>
        // POST: api/Negotiations
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [Authorize(Roles = "Customer")]
        public async Task<ActionResult<Negotiation>> PostNegotiation([FromBody] CreateNegotiationRequestDto requestDto)
        {
            var negotiation = await _service.CreateNegotiationAsync(requestDto);

            return CreatedAtAction(nameof(GetNegotiation), new { id = negotiation.NegotiationId }, negotiation);
        }

        /// <summary>
        /// Deletes a specific negotiation by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the negotiation to delete.</param>
        /// <returns>
        /// a 204 No Content response if the deletion is successful.
        /// </returns>
        // DELETE: api/Negotiation/5
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteNegotiation([FromRoute] NegotiationId id)
        {
            await _service.DeleteNegotiationAsync(id);

            return NoContent();
        }
    }
}
