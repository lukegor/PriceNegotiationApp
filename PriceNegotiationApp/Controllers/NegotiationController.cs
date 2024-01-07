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
using PriceNegotiationApp.Extensions;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Models.Input_Models;
using PriceNegotiationApp.Services;
using PriceNegotiationApp.Utility;
using static PriceNegotiationApp.Services.NegotiationService;

namespace PriceNegotiationApp.Controllers
{
	[Area("Negotiations")]
	[Route("api/v1/[area]/[controller]")]
	[ApiController]
	public class NegotiationController : ControllerBase
	{
		private readonly INegotiationService _service;

		public NegotiationController(INegotiationService service)
		{
			_service = service;
		}

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
		//DO POPRAWY
        [HttpGet("{id}")]
		[ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[Authorize(Policy = RequirementsNames.IsAdminOrStaffOrOwnerRequirement)]
		public async Task<ActionResult<Negotiation>> GetNegotiation([FromRoute] int id)
		{
			//if (!IsUserAuthorizedForNegotiation(id))
			//{
			//	return Forbid();
			//}

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
        /// a 400 Bad Request if the model state is invalid or if a concurrency conflict occurs,
        /// a 401 Unauthorized response if the user is unauthorized,
        /// a 403 Forbidden if the user is not authorized or does not possess the required role,
        /// a 404 Not Found if the specified negotiation is not found,
        /// or a 500 Internal Server Error for other errors.
        /// </returns>
        // PUT: api/Negotiation/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status500InternalServerError)]
		[Authorize(Roles = "Staff, Admin")]
		public async Task<IActionResult> PutNegotiation([FromRoute] int id, [FromBody] Negotiation negotiation)
        {
			var errors = ModelStateHelper.GetErrors(ModelState);
			if (errors.Any())
			{
				return BadRequest(errors);
			}


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
		/// <param name="negotiationId">The unique identifier of the negotiation to update.</param>
		/// <param name="proposedPrice">The proposed price for the negotiation.</param>
		// PATCH: api/Negotiations
		[HttpPatch]
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Authorize(Policy = RequirementsNames.IsOwnerRequirement)]
        public async Task<IActionResult> ProposeNewPrice(int negotiationId, decimal proposedPrice)
		{
			var response = await _service.ProposeNewPriceAsync(negotiationId, proposedPrice);

			return response.Result switch
			{
				ProposePriceResult.Success => Ok("Price proposed successfully."),
				ProposePriceResult.NotFound => NotFound("Negotiation not found."),
				ProposePriceResult.Unauthorized => Forbid("You are not authorized to propose a new price for this negotiation."),
				ProposePriceResult.IncorrectAction => BadRequest("No more retries are left for this negotiation."),
				ProposePriceResult.InvalidInput => BadRequest($"Invalid proposed price. Please note that the proposed price should be within the range of 0.01 - {response.MaxAllowedPrice}."),
				_ => StatusCode(500, "An error occurred while processing the proposal."),
			};
		}

		/// <summary>
		/// Responds to a negotiation proposal.
		/// </summary>
		/// <param name="negotiationId">The unique identifier of the negotiation to update.</param>
		/// <param name="isApproved">A flag indicating whether the proposal is approved or not.</param>
		/// <returns>Returns an <see cref="IActionResult"/> status code representing the result of the operation.</returns>
		[HttpPatch]
		[Route("response")]
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> RespondToNegotiationProposal(int negotiationId, [FromQuery] bool isApproved)
		{
			var result = await _service.RespondToNegotiationProposalAsync(negotiationId, isApproved);

			return result switch
			{
				UpdateResultType.Success => isApproved ? Ok("Proposal accepted") : Ok("Proposal rejected"),
				UpdateResultType.NotFound => NotFound(),
				UpdateResultType.Conflict => BadRequest(),
				_ => StatusCode(500, "Internal Server Error")
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
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
		[Authorize(Roles = "Customer")]
		public async Task<ActionResult<Negotiation>> PostNegotiation([FromBody] NegotiationInputModel negotiationDetails)
        {
			var errors = ModelStateHelper.GetErrors(ModelState);
			if (errors.Any())
			{
				return BadRequest(errors);
			}

			Negotiation negotiation = await _service.CreateNegotiationAsync(negotiationDetails);

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
		public async Task<IActionResult> DeleteNegotiation([FromRoute] int id)
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

        [Obsolete("Deprecated: Replaced by custom policies realizing resource-based authorization.")]
        private bool IsUserAssociatedWithNegotiation(int negotiationId)
		{
			return _service.IsUserAssociatedWithNegotiation(negotiationId);
		}

		/// <summary>
		/// if the authorized user is Customer, then check if the negotiation belongs to him; if user role is different then just return true
		/// </summary>
		/// <param name="negotiationId">The unique identifier of the negotiation</param>
		/// <returns></returns>
		[Obsolete("Deprecated: Replaced by custom policies realizing resource-based authorization.")]
		private bool IsUserAuthorizedForNegotiation(int negotiationId)
		{
			var userRole = _service.GetLoggedInUserRole();

			if (userRole == "Customer" && !IsUserAssociatedWithNegotiation(negotiationId))
			{
				return false;
			}

			return true;
		}
	}
}
