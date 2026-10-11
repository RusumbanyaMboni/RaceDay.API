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


        // =========================================================
        // GET: api/events/{eventId}/results
        // PUBLIC
        // =========================================================

        [AllowAnonymous]
        [HttpGet("events/{eventId}/results")]
        public async Task<IActionResult> GetEventResults(int eventId)
        {
            var eventExists = await _context.Events
                .AnyAsync(e => e.EventId == eventId);

            if (!eventExists)
            {
                return NotFound(new
                {
                    message = "Event not found."
                });
            }

            var results = await _context.Results
                .Where(r =>
                    r.Enrolment != null &&
                    r.Enrolment.EventId == eventId)
                .Select(r => new
                {
                    r.ResultId,
                    r.EnrolmentId,

                    participantId =
                        r.Enrolment!.ParticipantId,

                    participantName =
                        r.Enrolment.Participant!.FirstName +
                        " " +
                        r.Enrolment.Participant.LastName,

                    eventId =
                        r.Enrolment.EventId,

                    eventName =
                        r.Enrolment.Event!.EventName,

                    categoryId =
                        r.Enrolment.CategoryId,

                    categoryName =
                        r.Enrolment.Category!.CategoryName,

                    r.FinishTime,
                    r.FinishPosition,
                    r.RecordedAt
                })
                .OrderBy(r => r.FinishPosition)
                .ToListAsync();

            return Ok(results);
        }


        // =========================================================
        // GET: api/results/{id}
        // PUBLIC
        // =========================================================

        [AllowAnonymous]
        [HttpGet("results/{id}")]
        public async Task<IActionResult> GetResult(int id)
        {
            var result = await _context.Results
                .Where(r => r.ResultId == id)
                .Select(r => new
                {
                    r.ResultId,
                    r.EnrolmentId,

                    participantId =
                        r.Enrolment!.ParticipantId,

                    participantName =
                        r.Enrolment.Participant!.FirstName +
                        " " +
                        r.Enrolment.Participant.LastName,

                    eventId =
                        r.Enrolment.EventId,

                    eventName =
                        r.Enrolment.Event!.EventName,

                    categoryId =
                        r.Enrolment.CategoryId,

                    categoryName =
                        r.Enrolment.Category!.CategoryName,

                    r.FinishTime,
                    r.FinishPosition,
                    r.RecordedAt
                })
                .FirstOrDefaultAsync();

            if (result == null)
            {
                return NotFound(new
                {
                    message = "Result not found."
                });
            }

            return Ok(result);
        }


        // =========================================================
        // POST: api/enrolments/{enrolmentId}/results
        // ORGANIZER ONLY
        // =========================================================

        [Authorize(Roles = "1")]
        [HttpPost("enrolments/{enrolmentId}/results")]
        public async Task<IActionResult> CreateResult(
            int enrolmentId,
            [FromBody] CreateResultRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.FinishPosition <= 0)
            {
                return BadRequest(new
                {
                    message = "Finish position must be greater than zero."
                });
            }

            if (request.FinishTime <= TimeSpan.Zero)
            {
                return BadRequest(new
                {
                    message = "Finish time must be greater than zero."
                });
            }

            var enrolment = await _context.Enrolments
                .Include(e => e.Event)
                .FirstOrDefaultAsync(
                    e => e.EnrolmentId == enrolmentId);

            if (enrolment == null)
            {
                return NotFound(new
                {
                    message = "Enrolment not found."
                });
            }

            if (enrolment.Event == null)
            {
                return NotFound(new
                {
                    message = "Associated event not found."
                });
            }

            var organizerId = GetCurrentUserId();

            if (organizerId == null)
            {
                return Unauthorized(new
                {
                    message = "User ID could not be determined."
                });
            }

            // Organizer must own the event
            if (enrolment.Event.OrganiserId != organizerId.Value)
            {
                return Forbid();
            }

            var existingResult = await _context.Results
                .FirstOrDefaultAsync(
                    r => r.EnrolmentId == enrolmentId);

            if (existingResult != null)
            {
                return Conflict(new
                {
                    message =
                        "A result already exists for this enrolment."
                });
            }

            var result = new Result
            {
                EnrolmentId = enrolmentId,
                FinishTime = request.FinishTime,
                FinishPosition = request.FinishPosition,
                RecordedAt = DateTime.UtcNow
            };

            _context.Results.Add(result);

            enrolment.Status = "Completed";

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetResult),
                new
                {
                    id = result.ResultId
                },
                new
                {
                    message = "Result recorded successfully.",
                    resultId = result.ResultId
                });
        }


        // =========================================================
        // GET: api/results/my-results
        // PARTICIPANT ONLY
        // =========================================================

        [HttpGet("results/my-results")]
        public async Task<IActionResult> GetMyResults()
        {
            // -----------------------------------------------------
            // Get RoleId directly from JWT
            // -----------------------------------------------------

            var roleId =
                User.FindFirst("RoleId")?.Value;

            // -----------------------------------------------------
            // Make sure the logged-in user is a Participant
            // RoleId 2 = Participant
            // -----------------------------------------------------

            if (roleId != "2")
            {
                return Forbid();
            }

            // -----------------------------------------------------
            // Get current user ID
            // -----------------------------------------------------

            var participantId = GetCurrentUserId();

            if (participantId == null)
            {
                return Unauthorized(new
                {
                    message =
                        "User ID could not be determined from the JWT."
                });
            }

            // -----------------------------------------------------
            // Get only this participant's results
            // -----------------------------------------------------

            var results = await _context.Results
                .Where(r =>
                    r.Enrolment != null &&
                    r.Enrolment.ParticipantId ==
                    participantId.Value)
                .Select(r => new
                {
                    r.ResultId,
                    r.EnrolmentId,

                    participantId =
                        r.Enrolment!.ParticipantId,

                    eventId =
                        r.Enrolment.EventId,

                    eventName =
                        r.Enrolment.Event!.EventName,

                    categoryId =
                        r.Enrolment.CategoryId,

                    categoryName =
                        r.Enrolment.Category!.CategoryName,

                    r.FinishTime,
                    r.FinishPosition,
                    r.RecordedAt
                })
                .OrderBy(r => r.RecordedAt)
                .ToListAsync();

            return Ok(results);
        }


        // =========================================================
        // PUT: api/results/{id}
        // ORGANIZER ONLY
        // =========================================================

        [Authorize(Roles = "1")]
        [HttpPut("results/{id}")]
        public async Task<IActionResult> UpdateResult(
            int id,
            [FromBody] UpdateResultRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.FinishPosition <= 0)
            {
                return BadRequest(new
                {
                    message =
                        "Finish position must be greater than zero."
                });
            }

            if (request.FinishTime <= TimeSpan.Zero)
            {
                return BadRequest(new
                {
                    message =
                        "Finish time must be greater than zero."
                });
            }

            var result = await _context.Results
                .Include(r => r.Enrolment)
                    .ThenInclude(e => e!.Event)
                .FirstOrDefaultAsync(
                    r => r.ResultId == id);

            if (result == null)
            {
                return NotFound(new
                {
                    message = "Result not found."
                });
            }

            if (result.Enrolment == null ||
                result.Enrolment.Event == null)
            {
                return NotFound(new
                {
                    message =
                        "Associated enrolment or event not found."
                });
            }

            var organizerId = GetCurrentUserId();

            if (organizerId == null)
            {
                return Unauthorized(new
                {
                    message = "User ID could not be determined."
                });
            }

            if (result.Enrolment.Event.OrganiserId !=
                organizerId.Value)
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


        // =========================================================
        // DELETE: api/results/{id}
        // ORGANIZER ONLY
        // =========================================================

        [Authorize(Roles = "1")]
        [HttpDelete("results/{id}")]
        public async Task<IActionResult> DeleteResult(int id)
        {
            var result = await _context.Results
                .Include(r => r.Enrolment)
                   .ThenInclude(e => e!.Event)
                .FirstOrDefaultAsync(
                    r => r.ResultId == id);

            if (result == null)
            {
                return NotFound(new
                {
                    message = "Result not found."
                });
            }

            if (result.Enrolment == null ||
                result.Enrolment.Event == null)
            {
                return NotFound(new
                {
                    message =
                        "Associated enrolment or event not found."
                });
            }

            var organizerId = GetCurrentUserId();

            if (organizerId == null)
            {
                return Unauthorized(new
                {
                    message = "User ID could not be determined."
                });
            }

            if (result.Enrolment.Event.OrganiserId !=
                organizerId.Value)
            {
                return Forbid();
            }

            _context.Results.Remove(result);

            await _context.SaveChangesAsync();

            return NoContent();
        }


        // =========================================================
        // GET CURRENT USER ID FROM JWT
        // =========================================================

        private int? GetCurrentUserId()
        {
            var userIdClaim =
                User.FindFirst(
                    ClaimTypes.NameIdentifier)?.Value
                ??
                User.FindFirst("sub")?.Value
                ??
                User.FindFirst("UserId")?.Value;

            if (int.TryParse(
                userIdClaim,
                out int userId))
            {
                return userId;
            }

            return null;
        }


        // =========================================================
        // CREATE RESULT REQUEST
        // =========================================================

        public class CreateResultRequest
        {
            public TimeSpan FinishTime { get; set; }

            public int FinishPosition { get; set; }
        }


        // =========================================================
        // UPDATE RESULT REQUEST
        // =========================================================

        public class UpdateResultRequest
        {
            public TimeSpan FinishTime { get; set; }

            public int FinishPosition { get; set; }
        }
    }
}