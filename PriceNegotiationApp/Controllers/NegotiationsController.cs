using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Models;

namespace PriceNegotiationApp.Controllers
{
    [Area("Negotiation")]
    [Route("api/v1/[area]/[controller]")]
    [ApiController]
    public class NegotiationsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public NegotiationsController(AppDbContext context)
        {
            _context = context;
        }

		/// <summary>
		/// Retrieves a list of negotiations.
		/// </summary>
		/// <returns>Returns a collection of negotiations.</returns>
		// GET: api/Negotiations
		[HttpGet]
        [ResponseCache(Duration = 5)]
		public async Task<ActionResult<IEnumerable<Negotiation>>> GetNegotiations()
		{
			return await _context.Negotiations.ToListAsync();
		}

		/// <summary>
		/// Retrieves a specific negotiation by its unique identifier.
		/// </summary>
		/// <param name="id">The unique identifier of the negotiation to retrieve.</param>
		/// <returns>Returns a negotiation with the specified ID if found; otherwise, returns a 404 Not Found response.</returns>
		// GET: api/Negotiations/5
		[HttpGet("{id}")]
        public async Task<ActionResult<Negotiation>> GetNegotiation(int id)
        {
            var negotiation = await _context.Negotiations.FindAsync(id);

            if (negotiation == null)
            {
                return NotFound();
            }

            return negotiation;
        }

        // PUT: api/Negotiations/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutNegotiation(int id, Negotiation negotiation)
        {
            if (id != negotiation.Id)
            {
                return BadRequest();
            }

            _context.Entry(negotiation).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!NegotiationExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Negotiations
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Negotiation>> PostNegotiation(Negotiation negotiation)
        {
            _context.Negotiations.Add(negotiation);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetNegotiation), new { id = negotiation.Id }, negotiation);
        }

        // DELETE: api/Negotiations/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNegotiation(int id)
        {
            var negotiation = await _context.Negotiations.FindAsync(id);
            if (negotiation == null)
            {
                return NotFound();
            }

            _context.Negotiations.Remove(negotiation);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool NegotiationExists(int id)
        {
            return _context.Negotiations.Any(e => e.Id == id);
        }
    }
}
