
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
    public class CategoryController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public CategoryController(RaceDayDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // GET CATEGORIES FOR AN EVENT
        // Organiser and Participant
        // GET /api/events/{eventId}/categories
        // ==========================================

        [HttpGet("events/{eventId}/categories")]
        [Authorize(Roles = "Organiser,Participant")]
        public async Task<IActionResult> GetEventCategories(
            int eventId)
        {
            if (eventId <= 0)
                return BadRequest(new
                {
                    message = "Invalid event ID."
                });

            var eventExists = await _context.Events
                .AnyAsync(e => e.EventId == eventId);

            if (!eventExists)
            {
                return NotFound(new
                {
                    message = "Event not found."
                });
            }

            var categories = await _context.Categories
                .AsNoTracking()
                .Where(c => c.EventId == eventId)
                .OrderBy(c => c.Distance)
                .ThenBy(c => c.CategoryName)
                .Select(c => new
                {
                    c.CategoryId,
                    c.EventId,
                    c.CategoryName,
                    c.MinimumAge,
                    c.MaximumAge,
                    c.Distance
                })
                .ToListAsync();

            return Ok(categories);
        }

        // ==========================================
        // GET CATEGORY BY ID
        // Organiser and Participant
        // GET /api/categories/{id}
        // ==========================================

        [HttpGet("categories/{id}")]
        [Authorize(Roles = "Organiser,Participant")]
        public async Task<IActionResult> GetCategory(int id)
        {
            if (id <= 0)
                return BadRequest(new
                {
                    message = "Invalid category ID."
                });

            var category = await _context.Categories
                .AsNoTracking()
                .Where(c => c.CategoryId == id)
                .Select(c => new
                {
                    c.CategoryId,
                    c.EventId,
                    c.CategoryName,
                    c.MinimumAge,
                    c.MaximumAge,
                    c.Distance
                })
                .FirstOrDefaultAsync();

            if (category == null)
            {
                return NotFound(new
                {
                    message = "Category not found."
                });
            }

            return Ok(category);
        }

        // ==========================================
        // CREATE CATEGORY
        // Organiser only
        // POST /api/events/{eventId}/categories
        // ==========================================

        [HttpPost("events/{eventId}/categories")]
        [Authorize(Roles = "Organiser")]
        public async Task<IActionResult> CreateCategory(
            int eventId,
            [FromBody] Category category)
        {
            if (eventId <= 0)
                return BadRequest(new
                {
                    message = "Invalid event ID."
                });

            if (category == null)
                return BadRequest(new
                {
                    message = "Category information is required."
                });

            var organiserId = GetCurrentUserId();

            if (organiserId == null)
                return Unauthorized(new
                {
                    message = "Unable to identify the authenticated organiser."
                });

            var eventItem = await _context.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EventId == eventId);

            if (eventItem == null)
                return NotFound(new
                {
                    message = "Event not found."
                });

            // Only the owning Organiser can add categories.
            if (eventItem.OrganiserId != organiserId.Value)
                return Forbid();

            var validationError = ValidateCategory(category);

            if (validationError != null)
                return BadRequest(new
                {
                    message = validationError
                });

            // The event ID comes from the URL, not the body.
            category.CategoryId = 0;
            category.EventId = eventId;
            category.CategoryName = category.CategoryName.Trim();

            // Prevent clients from adding related entities
            // through the category creation request.
            category.Event = null;
            category.Enrolments = new List<Enrolment>();

            _context.Categories.Add(category);

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetCategory),
                new { id = category.CategoryId },
                new
                {
                    message = "Category created successfully.",
                    category = new
                    {
                        category.CategoryId,
                        category.EventId,
                        category.CategoryName,
                        category.MinimumAge,
                        category.MaximumAge,
                        category.Distance
                    }
                });
        }

        // ==========================================
        // UPDATE CATEGORY
        // Organiser only
        // PUT /api/categories/{id}
        // ==========================================

        [HttpPut("categories/{id}")]
        [Authorize(Roles = "Organiser")]
        public async Task<IActionResult> UpdateCategory(
            int id,
            [FromBody] Category updatedCategory)
        {
            if (id <= 0)
                return BadRequest(new
                {
                    message = "Invalid category ID."
                });

            if (updatedCategory == null)
                return BadRequest(new
                {
                    message = "Category information is required."
                });

            var organiserId = GetCurrentUserId();

            if (organiserId == null)
                return Unauthorized(new
                {
                    message = "Unable to identify the authenticated organiser."
                });

            var existingCategory = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == id);

            if (existingCategory == null)
                return NotFound(new
                {
                    message = "Category not found."
                });

            var eventItem = await _context.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    e.EventId == existingCategory.EventId);

            if (eventItem == null)
                return NotFound(new
                {
                    message = "Associated event not found."
                });

            // Only the owner may update this category.
            if (eventItem.OrganiserId != organiserId.Value)
                return Forbid();

            var validationError =
                ValidateCategory(updatedCategory);

            if (validationError != null)
                return BadRequest(new
                {
                    message = validationError
                });

            // Do not change CategoryId or EventId.
            existingCategory.CategoryName =
                updatedCategory.CategoryName.Trim();

            existingCategory.MinimumAge =
                updatedCategory.MinimumAge;

            existingCategory.MaximumAge =
                updatedCategory.MaximumAge;

            existingCategory.Distance =
                updatedCategory.Distance;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Category updated successfully.",
                category = new
                {
                    existingCategory.CategoryId,
                    existingCategory.EventId,
                    existingCategory.CategoryName,
                    existingCategory.MinimumAge,
                    existingCategory.MaximumAge,
                    existingCategory.Distance
                }
            });
        }

        // ==========================================
        // DELETE CATEGORY
        // Organiser only
        // DELETE /api/categories/{id}
        // ==========================================

        [HttpDelete("categories/{id}")]
        [Authorize(Roles = "Organiser")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            if (id <= 0)
                return BadRequest(new
                {
                    message = "Invalid category ID."
                });

            var organiserId = GetCurrentUserId();

            if (organiserId == null)
                return Unauthorized(new
                {
                    message = "Unable to identify the authenticated organiser."
                });

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == id);

            if (category == null)
                return NotFound(new
                {
                    message = "Category not found."
                });

            var eventItem = await _context.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    e.EventId == category.EventId);

            if (eventItem == null)
                return NotFound(new
                {
                    message = "Associated event not found."
                });

            // Only the event owner may delete its categories.
            if (eventItem.OrganiserId != organiserId.Value)
                return Forbid();

            // Avoid deleting categories that already
            // have registered participants.
            var hasEnrolments = await _context.Enrolments
                .AnyAsync(e => e.CategoryId == id);

            if (hasEnrolments)
            {
                return Conflict(new
                {
                    message =
                        "This category has participant enrolments and cannot be deleted."
                });
            }

            _context.Categories.Remove(category);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ==========================================
        // CATEGORY VALIDATION
        // ==========================================

        private static string? ValidateCategory(
            Category category)
        {
            if (string.IsNullOrWhiteSpace(category.CategoryName))
                return "Category name is required.";

            if (category.CategoryName.Trim().Length > 100)
                return "Category name cannot exceed 100 characters.";

            if (category.Distance <= 0)
                return "Category distance must be greater than zero.";

            if (category.MinimumAge.HasValue &&
                category.MinimumAge.Value < 0)
            {
                return "Minimum age cannot be negative.";
            }

            if (category.MaximumAge.HasValue &&
                category.MaximumAge.Value < 0)
            {
                return "Maximum age cannot be negative.";
            }

            if (category.MinimumAge.HasValue &&
                category.MaximumAge.HasValue &&
                category.MinimumAge.Value >
                category.MaximumAge.Value)
            {
                return "Minimum age cannot be greater than maximum age.";
            }

            return null;
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

            if (int.TryParse(userIdClaim, out int userId))
                return userId;

            return null;
        }
    }
}
