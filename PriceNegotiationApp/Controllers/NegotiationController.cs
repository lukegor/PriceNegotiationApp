using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Negotiations.Dto.Requests;
using PriceNegotiationApp.Services;
using PriceNegotiationApp.Services.Providers;
using PriceNegotiationApp.Utility.Utility;
using static PriceNegotiationApp.Services.NegotiationService;

namespace PriceNegotiationApp.Controllers
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
        /// Returns a 200 Ok response with a collection of negotiations,
        /// a 400 Bad Request response if the model state is invalid,
        /// a 401 Unauthorized response if the user is unauthorized,
        /// or a 403 Forbidden if the user is not authorized or does not possess the required role
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
        /// a 401 Unauthorized response if the user is unauthorized,
        /// or a 403 Forbidden if the user is not authorized or does not possess the required role,
        /// or a 404 Not Found response if the resource was not found</returns>
        // GET: api/Negotiations/5
        // TODO: DO POPRAWY
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Policy = RequirementsNames.IsAdminOrStaffOrOwnerRequirement)]
        public async Task<ActionResult<Negotiation>> GetNegotiation([FromRoute] string id)
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
        /// a 400 Bad Request if the model state is invalid,
        /// a 401 Unauthorized response if the user is unauthorized,
        /// a 403 Forbidden if the user is not authorized or does not possess the required role,
        /// a 404 Not Found if the specified negotiation is not found,
		/// a 409 Conflict if a concurrency conflict occurs,
        /// or a 500 Internal Server Error for other errors.
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
        public async Task<IActionResult> PutNegotiation([FromRoute] string id, [FromBody] UpdateNegotiationRequestDto negotiation)
        {
            var errors = ModelStateHelper.GetErrors(ModelState);
            if (errors.Any())
            {
                return BadRequest(errors);
            }

            var updateResult = await _service.UpdateNegotiationAsync(id, negotiation);

            return Ok(updateResult);
        }

        /// <summary>
        /// For owner to remove (close) negotiation he gave up on.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        // PUT: api/Negotiation/5/close
        [HttpPut("{id}/close")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Authorize(Policy = RequirementsNames.IsOwnerRequirement)]
        public async Task<IActionResult> CloseNegotiation([FromRoute] string id)
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
        [Authorize(Policy = RequirementsNames.IsOwnerRequirement)]
        public async Task<IActionResult> ProposeNewPrice([FromRoute] string negotiationId, decimal proposedPrice)
		{
			var response = await _service.ProposeNewPriceAsync(negotiationId, proposedPrice);

			return response.Result switch
			{
				ProposePriceResult.Success => Ok("ProductPrice proposed successfully."),
				ProposePriceResult.Failed => BadRequest($"Proposed price is too high. Max allowed price is {response.MaxAllowedPrice}."),
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
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
		[Authorize(Roles = "Customer")]
		public async Task<ActionResult<Negotiation>> PostNegotiation([FromBody] CreateNegotiationRequestDto requestDto)
        {
			var errors = ModelStateHelper.GetErrors(ModelState);
			if (errors.Any())
			{
				return BadRequest(errors);
			}

			Negotiation negotiation = await _service.CreateNegotiationAsync(requestDto);

            return CreatedAtAction(nameof(GetNegotiation), new { id = negotiation.Id }, negotiation);
        }

        /// <summary>
        /// Deletes a specific negotiation by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the negotiation to delete.</param>
        /// <returns>
        /// Returns a 404 Not Found response if the specified negotiation is not found,
        /// a 401 Unauthorized response if the user is unauthorized,
		/// a 403 Forbidden response if the user does not possess the required role
        /// or a 204 No Content response if the deletion is successful.
        /// </returns>
        // DELETE: api/Negotiation/5
        [HttpDelete("{id}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[Authorize(Roles = "Admin")]
		public async Task<IActionResult> DeleteNegotiation([FromRoute] string id)
        {
            await _service.DeleteNegotiationAsync(id);

            return NoContent();
        }
	}
}
