
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
    public class ResultController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public ResultController(RaceDayDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // VIEW EVENT RESULTS
        // GET /api/events/{eventId}/results
        // Public
        // ==========================================

        [AllowAnonymous]
        [HttpGet("events/{eventId}/results")]
        public async Task<IActionResult> GetEventResults(int eventId)
        {
            if (eventId <= 0)
                return BadRequest(new
                {
                    message = "Invalid event ID."
                });

            var eventExists = await _context.Events
                .AnyAsync(e => e.EventId == eventId);

            if (!eventExists)
                return NotFound(new
                {
                    message = "Event not found."
                });

            var results = await _context.Results
                .AsNoTracking()
                .Where(r =>
                    r.Enrolment != null &&
                    r.Enrolment.EventId == eventId)
                .Select(r => new
                {
                    r.ResultId,
                    r.EnrolmentId,

                    ParticipantId =
                        r.Enrolment!.ParticipantId,

                    ParticipantName =
                        r.Enrolment.Participant!.FirstName +
                        " " +
                        r.Enrolment.Participant.LastName,

                    EventId =
                        r.Enrolment.EventId,

                    EventName =
                        r.Enrolment.Event!.EventName,

                    CategoryId =
                        r.Enrolment.CategoryId,

                    CategoryName =
                        r.Enrolment.Category!.CategoryName,

                    r.FinishTime,
                    r.FinishPosition,
                    r.RecordedAt
                })
                .OrderBy(r => r.FinishPosition)
                .ToListAsync();

            return Ok(results);
        }

        // ==========================================
        // VIEW RESULT BY ID
        // GET /api/results/{id}
        // Public
        // ==========================================

        [AllowAnonymous]
        [HttpGet("results/{id}")]
        public async Task<IActionResult> GetResult(int id)
        {
            if (id <= 0)
                return BadRequest(new
                {
                    message = "Invalid result ID."
                });

            var result = await _context.Results
                .AsNoTracking()
                .Where(r => r.ResultId == id)
                .Select(r => new
                {
                    r.ResultId,
                    r.EnrolmentId,

                    ParticipantId =
                        r.Enrolment!.ParticipantId,

                    ParticipantName =
                        r.Enrolment.Participant!.FirstName +
                        " " +
                        r.Enrolment.Participant.LastName,

                    EventId =
                        r.Enrolment.EventId,

                    EventName =
                        r.Enrolment.Event!.EventName,

                    CategoryId =
                        r.Enrolment.CategoryId,

                    CategoryName =
                        r.Enrolment.Category!.CategoryName,

                    r.FinishTime,
                    r.FinishPosition,
                    r.RecordedAt
                })
                .FirstOrDefaultAsync();

            if (result == null)
                return NotFound(new
                {
                    message = "Result not found."
                });

            return Ok(result);
        }

        // ==========================================
        // CREATE RESULT
        // POST /api/enrolments/{enrolmentId}/results
        // Organiser only
        // ==========================================

        [HttpPost("enrolments/{enrolmentId}/results")]
        [Authorize(Roles = "Organiser")]
        public async Task<IActionResult> CreateResult(
            int enrolmentId,
            [FromBody] CreateResultRequest request)
        {
            if (enrolmentId <= 0)
                return BadRequest(new
                {
                    message = "Invalid enrolment ID."
                });

            if (request == null)
                return BadRequest(new
                {
                    message = "Result information is required."
                });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (request.FinishPosition <= 0)
                return BadRequest(new
                {
                    message =
                        "Finish position must be greater than zero."
                });

            if (request.FinishTime <= TimeSpan.Zero)
                return BadRequest(new
                {
                    message =
                        "Finish time must be greater than zero."
                });

            var enrolment = await _context.Enrolments
                .Include(e => e.Event)
                .FirstOrDefaultAsync(e =>
                    e.EnrolmentId == enrolmentId);

            if (enrolment == null)
                return NotFound(new
                {
                    message = "Enrolment not found."
                });

            if (enrolment.Event == null)
                return NotFound(new
                {
                    message = "Associated event not found."
                });

            var organiserId = GetCurrentUserId();

            if (organiserId == null)
                return Unauthorized(new
                {
                    message =
                        "User ID could not be determined."
                });

            // Only the Organiser who owns the event
            // may record results for its participants.
            if (enrolment.Event.OrganiserId != organiserId.Value)
                return Forbid();

            var existingResult = await _context.Results
                .AnyAsync(r =>
                    r.EnrolmentId == enrolmentId);

            if (existingResult)
                return Conflict(new
                {
                    message =
                        "A result already exists for this enrolment."
                });

            var result = new Result
            {
                EnrolmentId = enrolmentId,
                FinishTime = request.FinishTime,
                FinishPosition = request.FinishPosition,
                RecordedAt = DateTime.UtcNow
            };

            _context.Results.Add(result);

            // Preserve your existing workflow:
            // recording a result marks the enrolment
            // as Completed.
            enrolment.Status = "Completed";

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetResult),
                new { id = result.ResultId },
                new
                {
                    message = "Result recorded successfully.",
                    resultId = result.ResultId
                });
        }

        // ==========================================
        // MY RACE RESULTS
        // GET /api/results/my-results
        // Participant only
        // ==========================================

        [HttpGet("results/my-results")]
        [Authorize(Roles = "Participant")]
        public async Task<IActionResult> GetMyResults()
        {
            var participantId = GetCurrentUserId();

            if (participantId == null)
                return Unauthorized(new
                {
                    message =
                        "User ID could not be determined from the JWT."
                });

            var results = await _context.Results
                .AsNoTracking()
                .Where(r =>
                    r.Enrolment != null &&
                    r.Enrolment.ParticipantId ==
                        participantId.Value)
                .Select(r => new
                {
                    r.ResultId,
                    r.EnrolmentId,

                    ParticipantId =
                        r.Enrolment!.ParticipantId,

                    EventId =
                        r.Enrolment.EventId,

                    EventName =
                        r.Enrolment.Event!.EventName,

                    CategoryId =
                        r.Enrolment.CategoryId,

                    CategoryName =
                        r.Enrolment.Category!.CategoryName,

                    r.FinishTime,
                    r.FinishPosition,
                    r.RecordedAt
                })
                .OrderBy(r => r.RecordedAt)
                .ToListAsync();

            return Ok(results);
        }

        // ==========================================
        // UPDATE RESULT
        // PUT /api/results/{id}
        // Organiser only
        // ==========================================

        [HttpPut("results/{id}")]
        [Authorize(Roles = "Organiser")]
        public async Task<IActionResult> UpdateResult(
            int id,
            [FromBody] UpdateResultRequest request)
        {
            if (id <= 0)
                return BadRequest(new
                {
                    message = "Invalid result ID."
                });

            if (request == null)
                return BadRequest(new
                {
                    message = "Result information is required."
                });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (request.FinishPosition <= 0)
                return BadRequest(new
                {
                    message =
                        "Finish position must be greater than zero."
                });

            if (request.FinishTime <= TimeSpan.Zero)
                return BadRequest(new
                {
                    message =
                        "Finish time must be greater than zero."
                });

            var result = await _context.Results
                .Include(r => r.Enrolment)
                    .ThenInclude(e => e!.Event)
                .FirstOrDefaultAsync(r =>
                    r.ResultId == id);

            if (result == null)
                return NotFound(new
                {
                    message = "Result not found."
                });

            if (result.Enrolment == null ||
                result.Enrolment.Event == null)
            {
                return NotFound(new
                {
                    message =
                        "Associated enrolment or event not found."
                });
            }

            var organiserId = GetCurrentUserId();

            if (organiserId == null)
                return Unauthorized(new
                {
                    message =
                        "User ID could not be determined."
                });

            if (result.Enrolment.Event.OrganiserId !=
                organiserId.Value)
            {
                return Forbid();
            }

            result.FinishTime = request.FinishTime;
            result.FinishPosition = request.FinishPosition;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Result updated successfully.",
                resultId = result.ResultId
            });
        }

        // ==========================================
        // DELETE RESULT
        // DELETE /api/results/{id}
        // Organiser only
        // ==========================================

        [HttpDelete("results/{id}")]
        [Authorize(Roles = "Organiser")]
        public async Task<IActionResult> DeleteResult(int id)
        {
            if (id <= 0)
                return BadRequest(new
                {
                    message = "Invalid result ID."
                });

            var result = await _context.Results
                .Include(r => r.Enrolment)
                    .ThenInclude(e => e!.Event)
                .FirstOrDefaultAsync(r =>
                    r.ResultId == id);

            if (result == null)
                return NotFound(new
                {
                    message = "Result not found."
                });

            if (result.Enrolment == null ||
                result.Enrolment.Event == null)
            {
                return NotFound(new
                {
                    message =
                        "Associated enrolment or event not found."
                });
            }

            var organiserId = GetCurrentUserId();

            if (organiserId == null)
                return Unauthorized(new
                {
                    message =
                        "User ID could not be determined."
                });

            if (result.Enrolment.Event.OrganiserId !=
                organiserId.Value)
            {
                return Forbid();
            }

            _context.Results.Remove(result);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ==========================================
        // GET AUTHENTICATED USER ID
        // ==========================================

        private int? GetCurrentUserId()
        {
            var userIdClaim =
                User.FindFirst(
                    ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst("UserId")?.Value;

            if (int.TryParse(userIdClaim, out int userId))
                return userId;

            return null;
        }

        // ==========================================
        // CREATE RESULT REQUEST
        // ==========================================

        public class CreateResultRequest
        {
            public TimeSpan FinishTime { get; set; }

            public int FinishPosition { get; set; }
        }

        // ==========================================
        // UPDATE RESULT REQUEST
        // ==========================================

        public class UpdateResultRequest
        {
            public TimeSpan FinishTime { get; set; }

            public int FinishPosition { get; set; }
        }
    }
}
