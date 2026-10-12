using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.Models;
using System.Security.Claims;

namespace RaceDay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EventController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EventController(RaceDayDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // GET: api/Event
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetEvents()
        {
            var events = await _context.Events
                .Select(e => new
                {
                    e.EventId,
                    e.OrganiserId,
                    e.EventName,
                    e.Description,
                    e.EventDate,
                    e.Location,
                    e.Distance,
                    e.EventType,
                    e.CreatedAt
                })
                .ToListAsync();

            return Ok(events);
        }


        // =========================================================
        // GET: api/Event/1
        // =========================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEvent(int id)
        {
            var eventItem = await _context.Events
                .Where(e => e.EventId == id)
                .Select(e => new
                {
                    e.EventId,
                    e.OrganiserId,
                    e.EventName,
                    e.Description,
                    e.EventDate,
                    e.Location,
                    e.Distance,
                    e.EventType,
                    e.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (eventItem == null)
            {
                return NotFound("Event not found.");
            }

            return Ok(eventItem);
        }


        // =========================================================
        // POST: api/Event
        // =========================================================
        // Only Organisers can create events.
        // =========================================================
        [Authorize(Roles = "Organiser")]
        [HttpPost]
        public async Task<IActionResult> CreateEvent(
            [FromBody] Event eventItem)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // =====================================================
            // VALIDATE EVENT TYPE
            // =====================================================
            // Only run, walk and cycle are allowed.
            var validEventTypes = new[] { "run", "walk", "cycle" };

            if (string.IsNullOrWhiteSpace(eventItem.EventType) ||
                !validEventTypes.Contains(
                    eventItem.EventType.Trim(),
                    StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "Event type must be one of: run, walk, or cycle."
                });
            }

            // Normalize the value before saving it.
            eventItem.EventType =
                eventItem.EventType.Trim().ToLowerInvariant();

            // Get logged-in user's ID from JWT
            var userId = GetCurrentUserId();

            if (userId == null)
            {
                return Unauthorized("User ID could not be determined.");
            }

            // IMPORTANT:
            // Do not trust organiserId supplied by the client.
            // Always use the authenticated user's ID.
            eventItem.OrganiserId = userId.Value;

            // Created by the server
            eventItem.CreatedAt = DateTime.UtcNow;

            _context.Events.Add(eventItem);

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetEvent),
                new { id = eventItem.EventId },
                new
                {
                    message = "Event created successfully.",
                    eventId = eventItem.EventId
                });
        }


        // =========================================================
        // PUT: api/Event/1
        // =========================================================
        // Only Organisers can update events.
        // =========================================================
        [Authorize(Roles = "Organiser")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEvent(
            int id,
            [FromBody] Event eventItem)
        {
            if (id != eventItem.EventId)
            {
                return BadRequest("Event ID does not match.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // =====================================================
            // VALIDATE EVENT TYPE
            // =====================================================
            // Only run, walk and cycle are allowed.
            var validEventTypes = new[] { "run", "walk", "cycle" };

            if (string.IsNullOrWhiteSpace(eventItem.EventType) ||
                !validEventTypes.Contains(
                    eventItem.EventType.Trim(),
                    StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "Event type must be one of: run, walk, or cycle."
                });
            }

            // Normalize the value before saving it.
            eventItem.EventType =
                eventItem.EventType.Trim().ToLowerInvariant();

            // Get logged-in user's ID
            var userId = GetCurrentUserId();

            if (userId == null)
            {
                return Unauthorized("User ID could not be determined.");
            }

            // Find existing event
            var existingEvent = await _context.Events
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (existingEvent == null)
            {
                return NotFound("Event not found.");
            }

            // =====================================================
            // SECURITY CHECK
            // =====================================================
            // Only the organiser who owns the event can update it.
            if (existingEvent.OrganiserId != userId.Value)
            {
                return Forbid();
            }

            // Update event information
            existingEvent.EventName = eventItem.EventName;
            existingEvent.Description = eventItem.Description;
            existingEvent.EventDate = eventItem.EventDate;
            existingEvent.Location = eventItem.Location;
            existingEvent.Distance = eventItem.Distance;
            existingEvent.EventType = eventItem.EventType;

            // IMPORTANT:
            // OrganiserId is deliberately NOT updated.
            // Ownership cannot be transferred through this endpoint.

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Event updated successfully."
            });
        }


        // =========================================================
        // DELETE: api/Event/1
        // =========================================================
        // Only Organisers can delete events.
        // =========================================================
        [Authorize(Roles = "Organiser")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            // Get logged-in user's ID
            var userId = GetCurrentUserId();

            if (userId == null)
            {
                return Unauthorized("User ID could not be determined.");
            }

            // Find event
            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (eventItem == null)
            {
                return NotFound("Event not found.");
            }

            // =====================================================
            // SECURITY CHECK
            // =====================================================
            // Only the organiser who owns the event can delete it.
            if (eventItem.OrganiserId != userId.Value)
            {
                return Forbid();
            }

            // =====================================================
            // FIND RELATED RECORDS
            // =====================================================

            // Find all enrolments belonging to this event
            var enrolments = await _context.Enrolments
                .Where(e => e.EventId == id)
                .ToListAsync();

            // Get enrolment IDs
            var enrolmentIds = enrolments
                .Select(e => e.EnrolmentId)
                .ToList();

            // Find results belonging to those enrolments
            var results = new List<Result>();

            if (enrolmentIds.Count > 0)
            {
                results = await _context.Results
                    .Where(r => enrolmentIds.Contains(r.EnrolmentId))
                    .ToListAsync();
            }

            // Find categories belonging to this event
            var categories = await _context.Categories
                .Where(c => c.EventId == id)
                .ToListAsync();


            // =====================================================
            // DELETE IN CORRECT ORDER
            // =====================================================

            // 1. Delete Results first
            if (results.Count > 0)
            {
                _context.Results.RemoveRange(results);
            }

            // 2. Delete Enrolments second
            if (enrolments.Count > 0)
            {
                _context.Enrolments.RemoveRange(enrolments);
            }

            // 3. Delete Categories third
            if (categories.Count > 0)
            {
                _context.Categories.RemoveRange(categories);
            }

            // 4. Delete Event last
            _context.Events.Remove(eventItem);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Event deleted successfully."
            });
        }


        // =========================================================
        // GET CURRENT USER ID FROM JWT
        // =========================================================

        private int? GetCurrentUserId()
        {
            var userIdClaim =
                User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User?.FindFirst("sub")?.Value
                ?? User?.FindFirst("UserId")?.Value;

            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }

            return null;
        }
    }
}