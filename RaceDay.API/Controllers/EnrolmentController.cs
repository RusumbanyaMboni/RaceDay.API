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


        // =========================================================
        // POST: /api/events/{eventId}/enrolments
        //
        // Participant enrols in an event and selects a category.
        //
        // ROLE:
        // 2 = Participant
        //
        // ParticipantId comes from JWT.
        // EventId comes from route.
        // CategoryId comes from request body.
        // EnrolmentDate and Status are created by server.
        // =========================================================

        [HttpPost("events/{eventId}/enrolments")]
        [Authorize(Roles = "2")]
        public async Task<IActionResult> CreateEnrolment(
            int eventId,
            [FromBody] CreateEnrolmentRequest request)
        {
            // -----------------------------------------------------
            // Validate request
            // -----------------------------------------------------

            if (request == null || request.CategoryId <= 0)
            {
                return BadRequest(new
                {
                    message = "A valid categoryId is required."
                });
            }


            // -----------------------------------------------------
            // Get logged-in participant ID from JWT
            // -----------------------------------------------------

            var participantId = GetCurrentUserId();

            if (participantId == null)
            {
                return Unauthorized(new
                {
                    message = "Unable to identify the authenticated participant."
                });
            }


            // -----------------------------------------------------
            // Check that event exists
            // -----------------------------------------------------

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventId == eventId);

            if (eventItem == null)
            {
                return NotFound(new
                {
                    message = "Event not found."
                });
            }


            // -----------------------------------------------------
            // Check that category exists
            // -----------------------------------------------------

            var category = await _context.Categories
                .FirstOrDefaultAsync(c =>
                    c.CategoryId == request.CategoryId);

            if (category == null)
            {
                return NotFound(new
                {
                    message = "Category not found."
                });
            }


            // -----------------------------------------------------
            // Category must belong to selected event
            // -----------------------------------------------------

            if (category.EventId != eventId)
            {
                return BadRequest(new
                {
                    message =
                        "The selected category does not belong to this event."
                });
            }


            // -----------------------------------------------------
            // Prevent duplicate enrolment
            // -----------------------------------------------------

            var alreadyEnrolled = await _context.Enrolments
                .AnyAsync(e =>
                    e.EventId == eventId &&
                    e.ParticipantId == participantId.Value);

            if (alreadyEnrolled)
            {
                return Conflict(new
                {
                    message =
                        "You are already enrolled in this event."
                });
            }


            // -----------------------------------------------------
            // Create enrolment
            // -----------------------------------------------------

            var newEnrolment = new Enrolment
            {
                ParticipantId = participantId.Value,

                EventId = eventId,

                CategoryId = request.CategoryId,

                EnrolmentDate = DateTime.UtcNow,

                Status = "Registered"
            };


            // -----------------------------------------------------
            // Save enrolment
            // -----------------------------------------------------

            _context.Enrolments.Add(newEnrolment);

            await _context.SaveChangesAsync();


            // -----------------------------------------------------
            // Return 201 Created
            // -----------------------------------------------------

            return CreatedAtAction(
                nameof(GetEnrolment),
                new
                {
                    id = newEnrolment.EnrolmentId
                },
                new
                {
                    message = "Enrolment created successfully.",

                    enrolmentId =
                        newEnrolment.EnrolmentId,

                    participantId =
                        newEnrolment.ParticipantId,

                    eventId =
                        newEnrolment.EventId,

                    categoryId =
                        newEnrolment.CategoryId,

                    enrolmentDate =
                        newEnrolment.EnrolmentDate,

                    status =
                        newEnrolment.Status
                });
        }



        // =========================================================
        // GET: /api/enrolments/my-enrolments
        //
        // Participant can view their own enrolments only.
        //
        // ROLE:
        // 2 = Participant
        // =========================================================

        [HttpGet("enrolments/my-enrolments")]
        [Authorize(Roles = "2")]
        public async Task<IActionResult> GetMyEnrolments()
        {
            var participantId = GetCurrentUserId();

            if (participantId == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Unable to identify the authenticated participant."
                });
            }


            var enrolments = await _context.Enrolments
                .Where(e =>
                    e.ParticipantId == participantId.Value)

                .Select(e => new
                {
                    e.EnrolmentId,

                    e.ParticipantId,

                    e.EventId,

                    EventName =
                        e.Event != null
                            ? e.Event.EventName
                            : null,

                    e.CategoryId,

                    CategoryName =
                        e.Category != null
                            ? e.Category.CategoryName
                            : null,

                    e.EnrolmentDate,

                    e.Status
                })

                .ToListAsync();


            return Ok(enrolments);
        }



        // =========================================================
        // GET: /api/enrolments/{id}
        //
        // Participant:
        //      Can view their own enrolment.
        //
        // Organiser:
        //      Can view enrolments belonging to their own event.
        //
        // ROLE:
        // 1 = Organiser
        // 2 = Participant
        // =========================================================

        [HttpGet("enrolments/{id}")]
        [Authorize(Roles = "1,2")]
        public async Task<IActionResult> GetEnrolment(int id)
        {
            var currentUserId = GetCurrentUserId();

            if (currentUserId == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Unable to identify the authenticated user."
                });
            }


            var enrolment = await _context.Enrolments

                .Include(e => e.Event)

                .Include(e => e.Category)

                .FirstOrDefaultAsync(
                    e => e.EnrolmentId == id);


            if (enrolment == null)
            {
                return NotFound(new
                {
                    message = "Enrolment not found."
                });
            }


            // -----------------------------------------------------
            // Get RoleId from JWT
            //
            // 1 = Organiser
            // 2 = Participant
            // -----------------------------------------------------

            var roleId =
                User.FindFirst("RoleId")?.Value;


            // =====================================================
            // PARTICIPANT SECURITY CHECK
            // =====================================================

            if (roleId == "2")
            {
                if (enrolment.ParticipantId !=
                    currentUserId.Value)
                {
                    return Forbid();
                }
            }


            // =====================================================
            // ORGANISER SECURITY CHECK
            // =====================================================

            if (roleId == "1")
            {
                if (enrolment.Event == null ||
                    enrolment.Event.OrganiserId !=
                    currentUserId.Value)
                {
                    return Forbid();
                }
            }


            return Ok(new
            {
                enrolment.EnrolmentId,

                enrolment.ParticipantId,

                enrolment.EventId,

                eventName =
                    enrolment.Event?.EventName,

                enrolment.CategoryId,

                categoryName =
                    enrolment.Category?.CategoryName,

                enrolment.EnrolmentDate,

                enrolment.Status
            });
        }



        // =========================================================
        // GET: /api/events/{eventId}/enrolments
        //
        // Organiser can view enrolments for their own event only.
        //
        // ROLE:
        // 1 = Organiser
        // =========================================================

        [HttpGet("events/{eventId}/enrolments")]
        [Authorize(Roles = "1")]
        public async Task<IActionResult> GetEventEnrolments(
            int eventId)
        {
            var organizerId = GetCurrentUserId();

            if (organizerId == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Unable to identify the authenticated organizer."
                });
            }


            // -----------------------------------------------------
            // Check that event exists
            // -----------------------------------------------------

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(
                    e => e.EventId == eventId);


            if (eventItem == null)
            {
                return NotFound(new
                {
                    message = "Event not found."
                });
            }


            // -----------------------------------------------------
            // Organiser must own event
            // -----------------------------------------------------

            if (eventItem.OrganiserId !=
                organizerId.Value)
            {
                return Forbid();
            }


            // -----------------------------------------------------
            // Get event enrolments
            // -----------------------------------------------------

            var enrolments =
                await _context.Enrolments

                .Where(e =>
                    e.EventId == eventId)

                .Select(e => new
                {
                    e.EnrolmentId,

                    e.ParticipantId,

                    ParticipantEmail =
                        e.Participant != null
                            ? e.Participant.Email
                            : null,

                    e.EventId,

                    e.CategoryId,

                    CategoryName =
                        e.Category != null
                            ? e.Category.CategoryName
                            : null,

                    e.EnrolmentDate,

                    e.Status
                })

                .ToListAsync();


            return Ok(enrolments);
        }



        // =========================================================
        // PUT: /api/enrolments/{id}
        //
        // Participant:
        //      Can change their selected category.
        //
        // Organiser:
        //      Can change the enrolment status.
        //
        // ROLE:
        // 1 = Organiser
        // 2 = Participant
        // =========================================================

        [HttpPut("enrolments/{id}")]
        [Authorize(Roles = "1,2")]
        public async Task<IActionResult> UpdateEnrolment(
            int id,
            [FromBody] UpdateEnrolmentRequest request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    message =
                        "Request body is required."
                });
            }


            var currentUserId =
                GetCurrentUserId();


            if (currentUserId == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Unable to identify the authenticated user."
                });
            }


            // -----------------------------------------------------
            // Find enrolment
            // -----------------------------------------------------

            var enrolment =
                await _context.Enrolments

                .Include(e => e.Event)

                .FirstOrDefaultAsync(
                    e => e.EnrolmentId == id);


            if (enrolment == null)
            {
                return NotFound(new
                {
                    message =
                        "Enrolment not found."
                });
            }


            // -----------------------------------------------------
            // Get RoleId from JWT
            //
            // 1 = Organiser
            // 2 = Participant
            // -----------------------------------------------------

            var roleId =
                User.FindFirst("RoleId")?.Value;


            // =====================================================
            // PARTICIPANT
            // =====================================================

            if (roleId == "2")
            {
                // Participant can only update
                // their own enrolment

                if (enrolment.ParticipantId !=
                    currentUserId.Value)
                {
                    return Forbid();
                }


                // -------------------------------------------------
                // Category is required
                // -------------------------------------------------

                if (request.CategoryId == null ||
                    request.CategoryId <= 0)
                {
                    return BadRequest(new
                    {
                        message =
                            "A valid categoryId is required."
                    });
                }


                // -------------------------------------------------
                // Check category
                // -------------------------------------------------

                var category =
                    await _context.Categories

                    .FirstOrDefaultAsync(
                        c => c.CategoryId ==
                             request.CategoryId.Value);


                if (category == null)
                {
                    return NotFound(new
                    {
                        message =
                            "Category not found."
                    });
                }


                // -------------------------------------------------
                // Category must belong to same event
                // -------------------------------------------------

                if (category.EventId !=
                    enrolment.EventId)
                {
                    return BadRequest(new
                    {
                        message =
                            "The selected category does not belong to this event."
                    });
                }


                // -------------------------------------------------
                // Update category
                // -------------------------------------------------

                enrolment.CategoryId =
                    request.CategoryId.Value;
            }


            // =====================================================
            // ORGANISER
            // =====================================================

            else if (roleId == "1")
            {
                // Organiser can only update enrolments
                // belonging to their own event.

                if (enrolment.Event == null ||
                    enrolment.Event.OrganiserId !=
                    currentUserId.Value)
                {
                    return Forbid();
                }


                // -------------------------------------------------
                // Status is required
                // -------------------------------------------------

                if (string.IsNullOrWhiteSpace(
                    request.Status))
                {
                    return BadRequest(new
                    {
                        message =
                            "Status is required."
                    });
                }


                // -------------------------------------------------
                // Update status
                // -------------------------------------------------

                enrolment.Status =
                    request.Status;
            }


            return await SaveUpdatedEnrolment(
                enrolment);
        }



        // =========================================================
        // DELETE: /api/enrolments/{id}
        //
        // Participant:
        //      Can cancel their own enrolment.
        //
        // Organiser:
        //      Can remove an enrolment from their own event.
        //
        // ROLE:
        // 1 = Organiser
        // 2 = Participant
        // =========================================================

        [HttpDelete("enrolments/{id}")]
        [Authorize(Roles = "1,2")]
        public async Task<IActionResult> DeleteEnrolment(
            int id)
        {
            var currentUserId =
                GetCurrentUserId();


            if (currentUserId == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Unable to identify the authenticated user."
                });
            }


            // -----------------------------------------------------
            // Find enrolment
            // -----------------------------------------------------

            var enrolment =
                await _context.Enrolments

                .Include(e => e.Event)

                .FirstOrDefaultAsync(
                    e => e.EnrolmentId == id);


            if (enrolment == null)
            {
                return NotFound(new
                {
                    message =
                        "Enrolment not found."
                });
            }


            // -----------------------------------------------------
            // Get RoleId from JWT
            // -----------------------------------------------------

            var roleId =
                User.FindFirst("RoleId")?.Value;


            // =====================================================
            // PARTICIPANT
            // =====================================================

            if (roleId == "2")
            {
                // Participant can only delete
                // their own enrolment.

                if (enrolment.ParticipantId !=
                    currentUserId.Value)
                {
                    return Forbid();
                }
            }


            // =====================================================
            // ORGANISER
            // =====================================================

            else if (roleId == "1")
            {
                // Organiser can only delete enrolments
                // belonging to their own event.

                if (enrolment.Event == null ||
                    enrolment.Event.OrganiserId !=
                    currentUserId.Value)
                {
                    return Forbid();
                }
            }


            // -----------------------------------------------------
            // Delete enrolment
            // -----------------------------------------------------

            _context.Enrolments.Remove(
                enrolment);

            await _context.SaveChangesAsync();


            return NoContent();
        }



        // =========================================================
        // SAVE UPDATED ENROLMENT
        // =========================================================

        private async Task<IActionResult>
            SaveUpdatedEnrolment(
                Enrolment enrolment)
        {
            await _context.SaveChangesAsync();


            return Ok(new
            {
                message =
                    "Enrolment updated successfully.",

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



        // =========================================================
        // REQUEST MODEL FOR CREATE
        // =========================================================

        public class CreateEnrolmentRequest
        {
            public int CategoryId { get; set; }
        }



        // =========================================================
        // REQUEST MODEL FOR UPDATE
        // =========================================================

        public class UpdateEnrolmentRequest
        {
            public int? CategoryId { get; set; }

            public string? Status { get; set; }
        }



        // =========================================================
        // GET CURRENT USER ID FROM JWT
        // =========================================================

        private int? GetCurrentUserId()
        {
            var userIdClaim =
                User.FindFirst(
                    ClaimTypes.NameIdentifier)?.Value

                ?? User.FindFirst("sub")?.Value

                ?? User.FindFirst("UserId")?.Value;


            if (int.TryParse(
                userIdClaim,
                out int userId))
            {
                return userId;
            }


            return null;
        }
    }
}