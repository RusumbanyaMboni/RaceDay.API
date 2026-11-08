
using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace RaceDay.API.Controllers
{
    [ApiController]
    [Route("api/profile")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly AzureBlobStorageService _blobStorage;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(
            RaceDayDbContext context,
            AzureBlobStorageService blobStorage,
            ILogger<ProfileController> logger)
        {
            _context = context;
            _blobStorage = blobStorage;
            _logger = logger;
        }

        // ==========================================
        // GET CURRENT USER ID
        // ==========================================

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            return int.TryParse(claim, out var userId)
                ? userId
                : null;
        }

        // ==========================================
        // GET PROFILE
        // GET /api/profile
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized();

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u => u.UserId == userId.Value);

            if (user == null)
                return NotFound();

            return Ok(new
            {
                user.UserId,
                user.RoleId,
                user.FirstName,
                user.LastName,
                user.Email,
                user.PhoneNumber,
                user.DateOfBirth,
                user.CreatedAt,

                user.ProfilePictureBlobName,

                HasProfilePicture =
                    !string.IsNullOrWhiteSpace(
                        user.ProfilePictureBlobName)
            });
        }

        // ==========================================
        // UPDATE PROFILE
        // PUT /api/profile
        // ==========================================

        [HttpPut]
        public async Task<IActionResult> UpdateProfile(
            [FromBody] UpdateProfileRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized();

            var user = await _context.Users
                .FirstOrDefaultAsync(
                    u => u.UserId == userId.Value);

            if (user == null)
                return NotFound();

            user.FirstName = request.FirstName.Trim();
            user.LastName = request.LastName.Trim();
            user.PhoneNumber = request.PhoneNumber;
            user.DateOfBirth = request.DateOfBirth;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Profile updated successfully.",
                user.UserId,
                user.RoleId,
                user.FirstName,
                user.LastName,
                user.Email,
                user.PhoneNumber,
                user.DateOfBirth,
                user.CreatedAt,
                user.ProfilePictureBlobName
            });
        }

        // ==========================================
        // UPLOAD OR REPLACE PROFILE PICTURE
        // POST /api/profile/picture
        // ==========================================

        [HttpPost("picture")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> UploadProfilePicture(
            IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    message = "Please select an image."
                });
            }

            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized();

            var user = await _context.Users
                .FirstOrDefaultAsync(
                    u => u.UserId == userId.Value);

            if (user == null)
                return NotFound();

            string? newBlobName = null;

            try
            {
                // Remember the existing picture.
                var oldBlobName =
                    user.ProfilePictureBlobName;

                // Upload the new file to Azure.
                newBlobName =
                    await _blobStorage
                        .UploadProfilePictureAsync(
                            file,
                            userId.Value);

                // Store the new blob reference in SQL.
                user.ProfilePictureBlobName =
                    newBlobName;

                await _context.SaveChangesAsync();

                // Remove the old picture only after
                // the new reference has been saved.
                if (!string.IsNullOrWhiteSpace(oldBlobName) &&
                    oldBlobName != newBlobName)
                {
                    try
                    {
                        await _blobStorage.DeleteImageAsync(
                            "profile-pictures",
                            oldBlobName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Could not remove old picture for user {UserId}",
                            userId.Value);
                    }
                }

                return Ok(new
                {
                    message =
                        "Profile picture uploaded successfully.",

                    HasProfilePicture = true
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Database error saving profile picture for user {UserId}",
                    userId.Value);

                // Clean up a newly uploaded blob if
                // the database update failed.
                if (!string.IsNullOrWhiteSpace(newBlobName))
                {
                    try
                    {
                        await _blobStorage.DeleteImageAsync(
                            "profile-pictures",
                            newBlobName);
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogWarning(
                            cleanupEx,
                            "Could not clean up failed upload");
                    }
                }

                return StatusCode(500, new
                {
                    message =
                        "The picture was uploaded, but your profile could not be updated."
                });
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(
                    ex,
                    "Azure Storage error uploading picture for user {UserId}",
                    userId.Value);

                return StatusCode(503, new
                {
                    message =
                        "Image storage is temporarily unavailable."
                });
            }
        }

        // ==========================================
        // DOWNLOAD CURRENT PROFILE PICTURE
        // GET /api/profile/picture
        // ==========================================

        [HttpGet("picture")]
        public async Task<IActionResult> GetProfilePicture()
        {
            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized();

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u => u.UserId == userId.Value);

            if (user == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(
                user.ProfilePictureBlobName))
            {
                return NotFound(new
                {
                    message =
                        "No profile picture has been uploaded."
                });
            }

            try
            {
                var (content, contentType) =
                    await _blobStorage.DownloadImageAsync(
                        "profile-pictures",
                        user.ProfilePictureBlobName);

                Response.Headers.CacheControl =
                    "private, no-store";

                return File(content, contentType);
            }
            catch (RequestFailedException ex)
                when (ex.Status == 404)
            {
                return NotFound(new
                {
                    message = "Profile picture not found."
                });
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(
                    ex,
                    "Azure Storage error retrieving picture for user {UserId}",
                    userId.Value);

                return StatusCode(503, new
                {
                    message =
                        "Unable to retrieve the profile picture."
                });
            }
        }

        // ==========================================
        // DELETE PROFILE PICTURE
        // DELETE /api/profile/picture
        // ==========================================

        [HttpDelete("picture")]
        public async Task<IActionResult> DeleteProfilePicture()
        {
            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized();

            var user = await _context.Users
                .FirstOrDefaultAsync(
                    u => u.UserId == userId.Value);

            if (user == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(
                user.ProfilePictureBlobName))
            {
                return BadRequest(new
                {
                    message =
                        "There is no profile picture to delete."
                });
            }

            var blobName = user.ProfilePictureBlobName;

            try
            {
                // Delete the Azure image first.
                await _blobStorage.DeleteImageAsync(
                    "profile-pictures",
                    blobName);

                // Remove the SQL Server reference.
                user.ProfilePictureBlobName = null;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message =
                        "Profile picture deleted successfully.",
                    HasProfilePicture = false
                });
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(
                    ex,
                    "Azure Storage error deleting picture for user {UserId}",
                    userId.Value);

                return StatusCode(503, new
                {
                    message =
                        "Unable to delete the profile picture."
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Database error removing picture reference for user {UserId}",
                    userId.Value);

                return StatusCode(500, new
                {
                    message =
                        "The image was deleted, but the profile could not be updated."
                });
            }
        }
    }

    // ==========================================
    // UPDATE PROFILE REQUEST MODEL
    // ==========================================

    public class UpdateProfileRequest
    {
        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } =
            string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } =
            string.Empty;

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        public DateTime? DateOfBirth { get; set; }
    }
}
