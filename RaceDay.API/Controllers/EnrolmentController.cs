
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.Models;
using System.Security.Claims;

namespace RaceDay.API.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize]
    public class EnrolmentController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EnrolmentController(RaceDayDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // CREATE ENROLMENT
        // POST /api/events/{eventId}/enrolments
        // Participant only
        // ==========================================

        [HttpPost("events/{eventId}/enrolments")]
        [Authorize(Roles = "Participant")]
        public async Task<IActionResult> CreateEnrolment(
            int eventId,
            [FromBody] CreateEnrolmentRequest request)
        {
            if (eventId <= 0 ||
                request == null ||
                request.CategoryId <= 0)
            {
                return BadRequest(new
                {
                    message = "A valid event and category are required."
                });
            }

            var participantId = GetCurrentUserId();

            if (participantId == null)
                return Unauthorized();

            var eventItem = await _context.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EventId == eventId);

            if (eventItem == null)
                return NotFound(new
                {
                    message = "Event not found."
                });

            var category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.CategoryId == request.CategoryId);

            if (category == null)
                return NotFound(new
                {
                    message = "Category not found."
                });

            if (category.EventId != eventId)
                return BadRequest(new
                {
                    message =
                        "The selected category does not belong to this event."
                });

            var alreadyEnrolled = await _context.Enrolments
                .AnyAsync(e =>
                    e.EventId == eventId &&
                    e.ParticipantId == participantId.Value);

            if (alreadyEnrolled)
                return Conflict(new
                {
                    message = "You are already enrolled in this event."
                });

            var enrolment = new Enrolment
            {
                ParticipantId = participantId.Value,
                EventId = eventId,
                CategoryId = category.CategoryId,
                EnrolmentDate = DateTime.UtcNow,
                Status = "Registered"
            };

            _context.Enrolments.Add(enrolment);

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetEnrolment),
                new { id = enrolment.EnrolmentId },
                new
                {
                    message = "Enrolment created successfully.",
                    enrolment.EnrolmentId,
                    enrolment.ParticipantId,
                    enrolment.EventId,
                    enrolment.CategoryId,
                    enrolment.EnrolmentDate,
                    enrolment.Status
                });
        }

        // ==========================================
        // MY ENROLMENTS
        // GET /api/enrolments/my-enrolments
        // Participant only
        // ==========================================

        [HttpGet("enrolments/my-enrolments")]
        [Authorize(Roles = "Participant")]
        public async Task<IActionResult> GetMyEnrolments()
        {
            var participantId = GetCurrentUserId();

            if (participantId == null)
                return Unauthorized();

            var enrolments = await _context.Enrolments
                .AsNoTracking()
                .Where(e => e.ParticipantId == participantId.Value)
                .OrderByDescending(e => e.EnrolmentDate)
                .Select(e => new
                {
                    e.EnrolmentId,
                    e.ParticipantId,
                    e.EventId,
                    EventName = e.Event != null
                        ? e.Event.EventName
                        : null,
                    e.CategoryId,
                    CategoryName = e.Category != null
                        ? e.Category.CategoryName
                        : null,
                    e.EnrolmentDate,
                    e.Status
                })
                .ToListAsync();

            return Ok(enrolments);
        }

        // ==========================================
        // GET ENROLMENT BY ID
        // GET /api/enrolments/{id}
        // Organiser and Participant
        // ==========================================

        [HttpGet("enrolments/{id}")]
        [Authorize(Roles = "Organiser,Participant")]
        public async Task<IActionResult> GetEnrolment(int id)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized();

            var enrolment = await _context.Enrolments
                .AsNoTracking()
                .Include(e => e.Event)
                .Include(e => e.Category)
                .FirstOrDefaultAsync(e => e.EnrolmentId == id);

            if (enrolment == null)
                return NotFound(new
                {
                    message = "Enrolment not found."
                });

            if (User.IsInRole("Participant"))
            {
                if (enrolment.ParticipantId != userId.Value)
                    return Forbid();
            }
            else if (User.IsInRole("Organiser"))
            {
                if (enrolment.Event == null ||
                    enrolment.Event.OrganiserId != userId.Value)
                    return Forbid();
            }
            else
            {
                return Forbid();
            }

            return Ok(new
            {
                enrolment.EnrolmentId,
                enrolment.ParticipantId,
                enrolment.EventId,
                EventName = enrolment.Event?.EventName,
                enrolment.CategoryId,
                CategoryName = enrolment.Category?.CategoryName,
                enrolment.EnrolmentDate,
                enrolment.Status
            });
        }

        // ==========================================
        // EVENT ENROLMENTS
        // GET /api/events/{eventId}/enrolments
        // Organiser only
        // ==========================================

        [HttpGet("events/{eventId}/enrolments")]
        [Authorize(Roles = "Organiser")]
        public async Task<IActionResult> GetEventEnrolments(
            int eventId)
        {
            var organiserId = GetCurrentUserId();

            if (organiserId == null)
                return Unauthorized();

            var eventItem = await _context.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EventId == eventId);

            if (eventItem == null)
                return NotFound(new
                {
                    message = "Event not found."
                });

            if (eventItem.OrganiserId != organiserId.Value)
                return Forbid();

            var enrolments = await _context.Enrolments
                .AsNoTracking()
                .Where(e => e.EventId == eventId)
                .OrderByDescending(e => e.EnrolmentDate)
                .Select(e => new
                {
                    e.EnrolmentId,
                    e.ParticipantId,
                    ParticipantEmail = e.Participant != null
                        ? e.Participant.Email
                        : null,
                    e.EventId,
                    e.CategoryId,
                    CategoryName = e.Category != null
                        ? e.Category.CategoryName
                        : null,
                    e.EnrolmentDate,
                    e.Status
                })
                .ToListAsync();

            return Ok(enrolments);
        }

        // ==========================================
        // UPDATE ENROLMENT
        // PUT /api/enrolments/{id}
        // Participant changes category
        // Organiser changes status
        // ==========================================

        [HttpPut("enrolments/{id}")]
        [Authorize(Roles = "Organiser,Participant")]
        public async Task<IActionResult> UpdateEnrolment(
            int id,
            [FromBody] UpdateEnrolmentRequest request)
        {
            if (request == null)
                return BadRequest(new
                {
                    message = "Request body is required."
                });

            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized();

            var enrolment = await _context.Enrolments
                .Include(e => e.Event)
                .FirstOrDefaultAsync(e => e.EnrolmentId == id);

            if (enrolment == null)
                return NotFound(new
                {
                    message = "Enrolment not found."
                });

            if (User.IsInRole("Participant"))
            {
                if (enrolment.ParticipantId != userId.Value)
                    return Forbid();

                if (!request.CategoryId.HasValue ||
                    request.CategoryId.Value <= 0)
                {
                    return BadRequest(new
                    {
                        message = "A valid categoryId is required."
                    });
                }

                var category = await _context.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c =>
                        c.CategoryId == request.CategoryId.Value);

                if (category == null)
                    return NotFound(new
                    {
                        message = "Category not found."
                    });

                if (category.EventId != enrolment.EventId)
                    return BadRequest(new
                    {
                        message =
                            "The selected category does not belong to this event."
                    });

                enrolment.CategoryId = category.CategoryId;
            }
            else if (User.IsInRole("Organiser"))
            {
                if (enrolment.Event == null ||
                    enrolment.Event.OrganiserId != userId.Value)
                    return Forbid();

                if (string.IsNullOrWhiteSpace(request.Status))
                    return BadRequest(new
                    {
                        message = "Status is required."
                    });

                if (request.Status.Trim().Length > 30)
                    return BadRequest(new
                    {
                        message = "Status cannot exceed 30 characters."
                    });

                enrolment.Status = request.Status.Trim();
            }
            else
            {
                return Forbid();
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Enrolment updated successfully.",
                enrolment = new
                {
                    enrolment.EnrolmentId,
                    enrolment.ParticipantId,
                    enrolment.EventId,
                    enrolment.CategoryId,
                    enrolment.EnrolmentDate,
                    enrolment.Status
                }
            });
        }

        // ==========================================
        // DELETE ENROLMENT
        // DELETE /api/enrolments/{id}
        // Participant or owning Organiser
        // ==========================================

        [HttpDelete("enrolments/{id}")]
        [Authorize(Roles = "Organiser,Participant")]
        public async Task<IActionResult> DeleteEnrolment(int id)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized();

            var enrolment = await _context.Enrolments
                .Include(e => e.Event)
                .FirstOrDefaultAsync(e => e.EnrolmentId == id);

            if (enrolment == null)
                return NotFound(new
                {
                    message = "Enrolment not found."
                });

            if (User.IsInRole("Participant"))
            {
                if (enrolment.ParticipantId != userId.Value)
                    return Forbid();
            }
            else if (User.IsInRole("Organiser"))
            {
                if (enrolment.Event == null ||
                    enrolment.Event.OrganiserId != userId.Value)
                    return Forbid();
            }
            else
            {
                return Forbid();
            }

            _context.Enrolments.Remove(enrolment);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ==========================================
        // CREATE REQUEST MODEL
        // ==========================================

        public class CreateEnrolmentRequest
        {
            public int CategoryId { get; set; }
        }

        // ==========================================
        // UPDATE REQUEST MODEL
        // ==========================================

        public class UpdateEnrolmentRequest
        {
            public int? CategoryId { get; set; }

            public string? Status { get; set; }
        }

        // ==========================================
        // GET CURRENT USER ID FROM JWT
        // ==========================================

        private int? GetCurrentUserId()
        {
            var userIdClaim =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst("UserId")?.Value;

            if (int.TryParse(userIdClaim, out var userId))
                return userId;

            return null;
        }
    }
}
