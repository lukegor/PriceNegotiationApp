using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Services;
using PriceNegotiationApp.Utility;

namespace PriceNegotiationApp.Controllers
{
    [Area("Negotiation")]
    [Route("api/v1/[area]/[controller]")]
    [ApiController]
    public class NegotiationController : ControllerBase
    {
        private readonly NegotiationService _service;

		public NegotiationController(NegotiationService service)
        {
			_service = service;
        }

		/// <summary>
		/// Retrieves a list of all negotiations.
		/// </summary>
		/// <returns>Returns a collection of negotiations.</returns>
		// GET: api/Negotiations
		[HttpGet]
		[ResponseCache(Duration = 5)] //Caches the HTTP response for 5 seconds
		[ProducesResponseType(StatusCodes.Status200OK)]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<IEnumerable<Negotiation>>> GetNegotiations()
		{
			var negotiations = await _service.GetNegotiationsAsync();
            return Ok(negotiations);
		}

		/// <summary>
		/// Retrieves a specific negotiation by its unique identifier.
		/// </summary>
		/// <param name="id">The unique identifier of the negotiation to retrieve.</param>
		/// <returns>Returns a negotiation with the specified ID if found; otherwise, returns a 404 Not Found response.</returns>
		// GET: api/Negotiations/5
		[HttpGet("{id}")]
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[Authorize(Roles = "Admin, Customer")]
		public async Task<ActionResult<Negotiation>> GetNegotiation(int id)
        {
			if (!IsUserAssociatedWithNegotiation(id))
			{
				return StatusCode((int)HttpStatusCode.Forbidden);
			}

			var negotiation = await _service.GetNegotiationAsync(id);

            if (negotiation == null)
            {
                return NotFound();
            }

            return negotiation;
        }

		/// <summary>
		/// Updates a specific negotiation by its unique identifier.
		/// </summary>
		/// <param name="id">The unique identifier of the negotiation to update.</param>
		/// <param name="negotiation">The updated negotiation data.</param>
		/// <returns>
		/// Returns a 204 No Content response if the update is successful,
		/// 404 Not Found if the specified negotiation is not found,
		/// 400 Bad Request with a message "Concurrency conflict" if a concurrency conflict occurs,
		/// or a 500 Internal Server Error for other errors.
		/// </returns>
		// PUT: api/Negotiation/5
		// To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
		[HttpPut("{id}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status500InternalServerError)]
		[Authorize(Roles = "Customer")]
		public async Task<IActionResult> PutNegotiation(int id, Negotiation negotiation)
        {
			var updateResult = await _service.UpdateNegotiationAsync(id, negotiation);

			return updateResult switch
			{
				UpdateResultType.Success => NoContent(),// 204 No Content
				UpdateResultType.NotFound => NotFound(),// 404 Not Found
				UpdateResultType.Conflict => BadRequest("Concurrency conflict"),// 400 Bad Request
				_ => StatusCode(500, "Internal Server Error")// Handle other errors as a generic bad request
			};
		}

		/// <summary>
		/// Proposes a new price for a negotiation.
		/// </summary>
		/// <param name="negotiation">The negotiation to update.</param>
		/// <param name="proposedPrice">The proposed price for the negotiation.</param>
		// PATCH: api/Negotiations
		[HttpPatch]
		[Authorize(Roles = "Customer")]
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status500InternalServerError)]
		public async Task<IActionResult> ProposeNewPrice([FromBody] Negotiation negotiation, decimal proposedPrice)
		{
			var result = await _service.ProposeNewPriceAsync(negotiation.Id, proposedPrice);

			return result switch
			{
				ProposePriceResult.Success => Ok(negotiation),
				ProposePriceResult.NotFound => NotFound("Negotiation not found."),
				ProposePriceResult.Unauthorized => Forbid("You are not authorized to propose a new price for this negotiation."),
				ProposePriceResult.InvalidInput => BadRequest("Invalid negotiation or proposed price."),
				_ => StatusCode(500, "An error occurred while processing the proposal."),
			};
		}

		/// <summary>
		/// Creates a new negotiation.
		/// </summary>
		/// <param name="negotiationDetails">The negotiation data to be processed into negotiation.</param>
		/// <returns>
		/// Returns a 201 Created response with the newly created negotiation and a location header pointing to the negotiation,
		/// </returns>
		// POST: api/Negotiations
		// To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
		[HttpPost]
		[ProducesResponseType(StatusCodes.Status201Created)]
		[Authorize(Roles = "Customer")]
		public async Task<ActionResult<Negotiation>> PostNegotiation([FromBody] NegotiationInputModel negotiationDetails)
        {
			Negotiation negotiation = await _service.AddNegotiationToDbAsync(negotiationDetails);

            return CreatedAtAction(nameof(GetNegotiation), new { id = negotiation.Id }, negotiation);
        }

		/// <summary>
		/// Deletes a specific negotiation by its unique identifier.
		/// </summary>
		/// <param name="id">The unique identifier of the negotiation to delete.</param>
		/// <returns>
		/// Returns a 404 Not Found response if the specified negotiation is not found,
		/// or a 204 No Content response if the deletion is successful.
		/// </returns>
		// DELETE: api/Negotiation/5
		[HttpDelete("{id}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[Authorize(Roles = "Admin")]
		public async Task<IActionResult> DeleteNegotiation(int id)
        {
            var result = await _service.DeleteNegotiationAsync(id);

            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }

		/// <summary>
		/// Checks if a negotiation with the specified unique identifier exists.
		/// </summary>
		/// <param name="id">The unique identifier of the negotiation to check for existence.</param>
		/// <returns>Returns true if a negotiation with the specified ID exists; otherwise, returns false.</returns>
		private bool NegotiationExists(int id)
        {
            return _service.NegotiationExists(id);
        }

		private bool IsUserAssociatedWithNegotiation(int negotiationId)
		{
			return _service.IsUserAssociatedWithNegotiation(negotiationId);
		}
	}
}
