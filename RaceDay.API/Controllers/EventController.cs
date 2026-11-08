
using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.Models;
using RaceDay.API.Services;
using System.Security.Claims;

namespace RaceDay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EventController : ControllerBase
    {
        private const string EventImagesContainer = "event-images";

        private readonly RaceDayDbContext _context;
        private readonly AzureBlobStorageService _blobStorageService;
        private readonly ILogger<EventController> _logger;

        public EventController(
            RaceDayDbContext context,
            AzureBlobStorageService blobStorageService,
            ILogger<EventController> logger)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _logger = logger;
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
                    e.CreatedAt,

                    // Azure image information
                    e.EventImageBlobName,
                    HasEventImage =
                        e.EventImageBlobName != null &&
                        e.EventImageBlobName != ""
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
                    e.CreatedAt,

                    // Azure image information
                    e.EventImageBlobName,
                    HasEventImage =
                        e.EventImageBlobName != null &&
                        e.EventImageBlobName != ""
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

            // Validate event type.
            var validEventTypes = new[]
            {
                "run",
                "walk",
                "cycle"
            };

            if (string.IsNullOrWhiteSpace(eventItem.EventType) ||
                !validEventTypes.Contains(
                    eventItem.EventType.Trim(),
                    StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message =
                        "Event type must be one of: run, walk, or cycle."
                });
            }

            eventItem.EventType =
                eventItem.EventType.Trim().ToLowerInvariant();

            var userId = GetCurrentUserId();

            if (userId == null)
            {
                return Unauthorized(
                    "User ID could not be determined.");
            }

            // Never trust organiser ownership from the request.
            eventItem.OrganiserId = userId.Value;

            // Server-generated creation date.
            eventItem.CreatedAt = DateTime.UtcNow;

            // Images must be uploaded separately using
            // POST /api/Event/{id}/image.
            // Do not accept a blob reference from the client.
            eventItem.EventImageBlobName = null;

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
        // Only the event's Organiser can update it.
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

            var validEventTypes = new[]
            {
                "run",
                "walk",
                "cycle"
            };

            if (string.IsNullOrWhiteSpace(eventItem.EventType) ||
                !validEventTypes.Contains(
                    eventItem.EventType.Trim(),
                    StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message =
                        "Event type must be one of: run, walk, or cycle."
                });
            }

            eventItem.EventType =
                eventItem.EventType.Trim().ToLowerInvariant();

            var userId = GetCurrentUserId();

            if (userId == null)
            {
                return Unauthorized(
                    "User ID could not be determined.");
            }

            var existingEvent = await _context.Events
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (existingEvent == null)
            {
                return NotFound("Event not found.");
            }

            // Only the owner may update this event.
            if (existingEvent.OrganiserId != userId.Value)
            {
                return Forbid();
            }

            // Update normal event information.
            existingEvent.EventName = eventItem.EventName;
            existingEvent.Description = eventItem.Description;
            existingEvent.EventDate = eventItem.EventDate;
            existingEvent.Location = eventItem.Location;
            existingEvent.Distance = eventItem.Distance;
            existingEvent.EventType = eventItem.EventType;

            // Do not change:
            // OrganiserId
            // CreatedAt
            // EventImageBlobName
            //
            // Event images use dedicated endpoints.

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Event updated successfully."
            });
        }

        // =========================================================
        // DELETE: api/Event/1
        // Only the event's Organiser can delete it.
        // =========================================================

        [Authorize(Roles = "Organiser")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
            {
                return Unauthorized(
                    "User ID could not be determined.");
            }

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (eventItem == null)
            {
                return NotFound("Event not found.");
            }

            if (eventItem.OrganiserId != userId.Value)
            {
                return Forbid();
            }

            // Keep a copy for Azure cleanup after
            // SQL Server deletion succeeds.
            var imageBlobName = eventItem.EventImageBlobName;

            // Find related enrolments.
            var enrolments = await _context.Enrolments
                .Where(e => e.EventId == id)
                .ToListAsync();

            var enrolmentIds = enrolments
                .Select(e => e.EnrolmentId)
                .ToList();

            // Find results for those enrolments.
            var results = new List<Result>();

            if (enrolmentIds.Count > 0)
            {
                results = await _context.Results
                    .Where(r =>
                        enrolmentIds.Contains(r.EnrolmentId))
                    .ToListAsync();
            }

            // Find event categories.
            var categories = await _context.Categories
                .Where(c => c.EventId == id)
                .ToListAsync();

            // Delete records in the required order.
            if (results.Count > 0)
            {
                _context.Results.RemoveRange(results);
            }

            if (enrolments.Count > 0)
            {
                _context.Enrolments.RemoveRange(enrolments);
            }

            if (categories.Count > 0)
            {
                _context.Categories.RemoveRange(categories);
            }

            _context.Events.Remove(eventItem);

            await _context.SaveChangesAsync();

            // After SQL Server confirms deletion,
            // attempt to clean up the Azure image.
            if (!string.IsNullOrWhiteSpace(imageBlobName))
            {
                try
                {
                    await _blobStorageService.DeleteImageAsync(
                        EventImagesContainer,
                        imageBlobName);
                }
                catch (RequestFailedException ex)
                {
                    // The event is already deleted from SQL Server.
                    // Log the orphaned blob for later cleanup.
                    _logger.LogError(
                        ex,
                        "Event {EventId} was deleted, but Azure blob {BlobName} could not be removed.",
                        id,
                        imageBlobName);
                }
            }

            return Ok(new
            {
                message = "Event deleted successfully."
            });
        }

        // =========================================================
        // POST: api/Event/1/image
        // Upload or replace an event image.
        //
        // Only the organiser who owns the event
        // can upload or replace its image.
        // =========================================================

        [Authorize(Roles = "Organiser")]
        [HttpPost("{id}/image")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> UploadEventImage(
            int id,
            IFormFile file)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
            {
                return Unauthorized(
                    "User ID could not be determined.");
            }

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (eventItem == null)
            {
                return NotFound(new
                {
                    message = "Event not found."
                });
            }

            // Ownership check.
            if (eventItem.OrganiserId != userId.Value)
            {
                return Forbid();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    message = "Please select an image."
                });
            }

            var oldBlobName = eventItem.EventImageBlobName;
            string? newBlobName = null;

            try
            {
                // Existing Azure service validates
                // image size, extension and signature.
                newBlobName =
                    await _blobStorageService.UploadEventImageAsync(
                        file,
                        id);

                // Store the private blob reference in SQL Server.
                eventItem.EventImageBlobName = newBlobName;

                await _context.SaveChangesAsync();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(
                    ex,
                    "Azure upload failed for event {EventId}.",
                    id);

                return StatusCode(503, new
                {
                    message =
                        "Azure Storage is currently unavailable."
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Database update failed after image upload for event {EventId}.",
                    id);

                // Prevent an unnecessary Azure blob
                // if the database update failed.
                if (!string.IsNullOrWhiteSpace(newBlobName))
                {
                    try
                    {
                        await _blobStorageService.DeleteImageAsync(
                            EventImagesContainer,
                            newBlobName);
                    }
                    catch (RequestFailedException cleanupException)
                    {
                        _logger.LogError(
                            cleanupException,
                            "Could not clean up uploaded blob {BlobName}.",
                            newBlobName);
                    }
                }

                return StatusCode(500, new
                {
                    message =
                        "Unable to save the event image information."
                });
            }

            // If the image was replaced, remove the old blob.
            if (!string.IsNullOrWhiteSpace(oldBlobName) &&
                oldBlobName != newBlobName)
            {
                try
                {
                    await _blobStorageService.DeleteImageAsync(
                        EventImagesContainer,
                        oldBlobName);
                }
                catch (RequestFailedException ex)
                {
                    // The new image is already saved.
                    _logger.LogWarning(
                        ex,
                        "Event {EventId} image was replaced, but old blob {BlobName} could not be deleted.",
                        id,
                        oldBlobName);
                }
            }

            return Ok(new
            {
                message =
                    "Event image uploaded successfully.",
                eventId = id,
                hasEventImage = true
            });
        }

        // =========================================================
        // GET: api/Event/1/image
        // Retrieve an image from private Azure Blob Storage.
        //
        // Any authenticated user may view the image.
        // =========================================================

        [HttpGet("{id}/image")]
        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public async Task<IActionResult> GetEventImage(int id)
        {
            var eventItem = await _context.Events
                .AsNoTracking()
                .Where(e => e.EventId == id)
                .Select(e => new
                {
                    e.EventImageBlobName
                })
                .FirstOrDefaultAsync();

            if (eventItem == null)
            {
                return NotFound(new
                {
                    message = "Event not found."
                });
            }

            if (string.IsNullOrWhiteSpace(
                eventItem.EventImageBlobName))
            {
                return NotFound(new
                {
                    message = "This event has no image."
                });
            }

            try
            {
                var image =
                    await _blobStorageService.DownloadImageAsync(
                        EventImagesContainer,
                        eventItem.EventImageBlobName);

                Response.Headers.CacheControl =
                    "private, no-store";

                return File(
                    image.Content,
                    image.ContentType);
            }
            catch (RequestFailedException ex)
                when (ex.Status == 404)
            {
                return NotFound(new
                {
                    message =
                        "The event image was not found in Azure Storage."
                });
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(
                    ex,
                    "Unable to retrieve image for event {EventId}.",
                    id);

                return StatusCode(503, new
                {
                    message =
                        "Unable to retrieve the event image."
                });
            }
        }

        // =========================================================
        // DELETE: api/Event/1/image
        // Remove an event image.
        //
        // Only the organiser who owns the event
        // can remove its image.
        // =========================================================

        [Authorize(Roles = "Organiser")]
        [HttpDelete("{id}/image")]
        public async Task<IActionResult> DeleteEventImage(int id)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
            {
                return Unauthorized(
                    "User ID could not be determined.");
            }

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (eventItem == null)
            {
                return NotFound(new
                {
                    message = "Event not found."
                });
            }

            if (eventItem.OrganiserId != userId.Value)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(
                eventItem.EventImageBlobName))
            {
                return NotFound(new
                {
                    message = "This event has no image to remove."
                });
            }

            var blobName = eventItem.EventImageBlobName;

            try
            {
                // First remove the Azure blob.
                await _blobStorageService.DeleteImageAsync(
                    EventImagesContainer,
                    blobName);

                // Then remove its SQL Server reference.
                eventItem.EventImageBlobName = null;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message =
                        "Event image removed successfully.",
                    eventId = id,
                    hasEventImage = false
                });
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(
                    ex,
                    "Unable to delete Azure image for event {EventId}.",
                    id);

                return StatusCode(503, new
                {
                    message =
                        "Unable to remove the image from Azure Storage."
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Database update failed after deleting the image for event {EventId}.",
                    id);

                return StatusCode(500, new
                {
                    message =
                        "The image was removed from storage, but the database could not be updated."
                });
            }
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
