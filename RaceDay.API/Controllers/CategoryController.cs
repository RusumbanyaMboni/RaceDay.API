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

        // =========================================================
        // GET CATEGORIES FOR AN EVENT
        // Organizers and Participants can view categories
        // GET: /api/events/{eventId}/categories
        // =========================================================
        [HttpGet("events/{eventId}/categories")]
        [Authorize(Roles = "1,2")]
        public async Task<IActionResult> GetEventCategories(int eventId)
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

            var categories = await _context.Categories
                .Where(c => c.EventId == eventId)
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

        // =========================================================
        // GET CATEGORY BY ID
        // Organizers and Participants can view a category
        // GET: /api/categories/{id}
        // =========================================================
        [HttpGet("categories/{id}")]
        [Authorize(Roles = "1,2")]
        public async Task<IActionResult> GetCategory(int id)
        {
            var category = await _context.Categories
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

        // =========================================================
        // CREATE CATEGORY
        // Organizer only
        // POST: /api/events/{eventId}/categories
        // =========================================================
        [HttpPost("events/{eventId}/categories")]
        [Authorize(Roles = "1")]
        public async Task<IActionResult> CreateCategory(
            int eventId,
            Category category)
        {
            var organizerId = GetCurrentUserId();

            if (organizerId == null)
            {
                return Unauthorized(new
                {
                    message = "Unable to identify the authenticated organizer."
                });
            }

            // Check that the event exists
            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventId == eventId);

            if (eventItem == null)
            {
                return NotFound(new
                {
                    message = "Event not found."
                });
            }

            // Check event ownership
            if (eventItem.OrganiserId != organizerId.Value)
            {
                return Forbid();
            }

            // Basic validation
            if (string.IsNullOrWhiteSpace(category.CategoryName))
            {
                return BadRequest(new
                {
                    message = "Category name is required."
                });
            }

            if (category.Distance <= 0)
            {
                return BadRequest(new
                {
                    message = "Category distance must be greater than zero."
                });
            }

            if (category.MinimumAge.HasValue &&
                category.MaximumAge.HasValue &&
                category.MinimumAge > category.MaximumAge)
            {
                return BadRequest(new
                {
                    message = "Minimum age cannot be greater than maximum age."
                });
            }

            // Route determines the EventId.
            // Do not allow the request body to override it.
            category.CategoryId = 0;
            category.EventId = eventId;

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

        // =========================================================
        // UPDATE CATEGORY
        // Organizer only
        // PUT: /api/categories/{id}
        // =========================================================
        [HttpPut("categories/{id}")]
        [Authorize(Roles = "1")]
        public async Task<IActionResult> UpdateCategory(
            int id,
            Category updatedCategory)
        {
            var organizerId = GetCurrentUserId();

            if (organizerId == null)
            {
                return Unauthorized(new
                {
                    message = "Unable to identify the authenticated organizer."
                });
            }

            var existingCategory = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == id);

            if (existingCategory == null)
            {
                return NotFound(new
                {
                    message = "Category not found."
                });
            }

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventId == existingCategory.EventId);

            if (eventItem == null)
            {
                return NotFound(new
                {
                    message = "Associated event not found."
                });
            }

            // Only the owner of the event can modify its categories
            if (eventItem.OrganiserId != organizerId.Value)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(updatedCategory.CategoryName))
            {
                return BadRequest(new
                {
                    message = "Category name is required."
                });
            }

            if (updatedCategory.Distance <= 0)
            {
                return BadRequest(new
                {
                    message = "Category distance must be greater than zero."
                });
            }

            if (updatedCategory.MinimumAge.HasValue &&
                updatedCategory.MaximumAge.HasValue &&
                updatedCategory.MinimumAge > updatedCategory.MaximumAge)
            {
                return BadRequest(new
                {
                    message = "Minimum age cannot be greater than maximum age."
                });
            }

            existingCategory.CategoryName = updatedCategory.CategoryName;
            existingCategory.MinimumAge = updatedCategory.MinimumAge;
            existingCategory.MaximumAge = updatedCategory.MaximumAge;
            existingCategory.Distance = updatedCategory.Distance;

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

        // =========================================================
        // DELETE CATEGORY
        // Organizer only
        // DELETE: /api/categories/{id}
        // =========================================================
        [HttpDelete("categories/{id}")]
        [Authorize(Roles = "1")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var organizerId = GetCurrentUserId();

            if (organizerId == null)
            {
                return Unauthorized(new
                {
                    message = "Unable to identify the authenticated organizer."
                });
            }

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == id);

            if (category == null)
            {
                return NotFound(new
                {
                    message = "Category not found."
                });
            }

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.EventId == category.EventId);

            if (eventItem == null)
            {
                return NotFound(new
                {
                    message = "Associated event not found."
                });
            }

            // Only the organizer who owns the event can delete
            if (eventItem.OrganiserId != organizerId.Value)
            {
                return Forbid();
            }

            _context.Categories.Remove(category);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // =========================================================
        // GET CURRENT USER ID FROM JWT
        // =========================================================
        private int? GetCurrentUserId()
        {
            var userIdClaim =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst("UserId")?.Value;

            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }

            return null;
        }
    }
}