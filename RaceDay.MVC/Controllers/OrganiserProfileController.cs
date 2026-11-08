
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.MVC.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.MVC.Controllers
{
    [Authorize(Roles = "Organiser")]
    public class OrganiserProfileController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public OrganiserProfileController(
            IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // ==========================================
        // AUTHENTICATED API CLIENT
        // ==========================================

        private HttpClient CreateApiClient()
        {
            var client = _httpClientFactory
                .CreateClient("RaceDayAPI");

            var token = HttpContext.Session
                .GetString("JwtToken");

            if (!string.IsNullOrWhiteSpace(token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer", token);
            }

            return client;
        }

        private bool HasApiToken()
        {
            return !string.IsNullOrWhiteSpace(
                HttpContext.Session.GetString("JwtToken"));
        }

        private async Task<IActionResult> LoginAgain()
        {
            await HttpContext.SignOutAsync("RaceDayCookie");

            HttpContext.Session.Clear();

            return RedirectToAction("Login", "Account");
        }

        // ==========================================
        // VIEW PROFILE
        // GET /OrganiserProfile/Index
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!HasApiToken())
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                using var response = await client.GetAsync(
                    "api/profile");

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (response.StatusCode ==
                    HttpStatusCode.Forbidden)
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                if (!response.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to load your profile.";

                    return RedirectToAction(
                        "Index", "Organiser");
                }

                var profile = await response.Content
                    .ReadFromJsonAsync<ParticipantProfileViewModel>();

                if (profile == null)
                    return NotFound();

                return View(profile);
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] =
                    "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                TempData["ErrorMessage"] =
                    "The profile request timed out.";
            }
            catch (JsonException)
            {
                TempData["ErrorMessage"] =
                    "Unable to process profile information.";
            }

            return RedirectToAction(
                "Index", "Organiser");
        }

        // ==========================================
        // EDIT PROFILE - GET
        // GET /OrganiserProfile/Edit
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            if (!HasApiToken())
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                using var response = await client.GetAsync(
                    "api/profile");

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (response.StatusCode ==
                    HttpStatusCode.Forbidden)
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                if (!response.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to load your profile.";

                    return RedirectToAction(nameof(Index));
                }

                var profile = await response.Content
                    .ReadFromJsonAsync<ParticipantProfileViewModel>();

                if (profile == null)
                    return NotFound();

                return View(profile);
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] =
                    "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                TempData["ErrorMessage"] =
                    "The profile request timed out.";
            }
            catch (JsonException)
            {
                TempData["ErrorMessage"] =
                    "Unable to process profile information.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // EDIT PROFILE - POST
        // POST /OrganiserProfile/Edit
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            ParticipantProfileViewModel model)
        {
            if (!HasApiToken())
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                // Retrieve account data from the API.
                // Do not trust user IDs, roles or email
                // addresses submitted by the browser.

                using var profileResponse =
                    await client.GetAsync("api/profile");

                if (profileResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (profileResponse.StatusCode ==
                    HttpStatusCode.Forbidden)
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                if (!profileResponse.IsSuccessStatusCode)
                {
                    ModelState.AddModelError(
                        "",
                        "Unable to verify your profile.");

                    return View(model);
                }

                var existingProfile =
                    await profileResponse.Content
                        .ReadFromJsonAsync<ParticipantProfileViewModel>();

                if (existingProfile == null)
                    return NotFound();

                model.UserId = existingProfile.UserId;
                model.RoleId = existingProfile.RoleId;
                model.Email = existingProfile.Email;
                model.CreatedAt = existingProfile.CreatedAt;

                ModelState.Remove(nameof(model.UserId));
                ModelState.Remove(nameof(model.RoleId));
                ModelState.Remove(nameof(model.Email));
                ModelState.Remove(nameof(model.CreatedAt));

                if (!ModelState.IsValid)
                    return View(model);

                var request = new
                {
                    FirstName = model.FirstName.Trim(),

                    LastName = model.LastName.Trim(),

                    PhoneNumber =
                        string.IsNullOrWhiteSpace(model.PhoneNumber)
                            ? null
                            : model.PhoneNumber.Trim(),

                    DateOfBirth = model.DateOfBirth
                };

                using var updateResponse =
                    await client.PutAsJsonAsync(
                        "api/profile",
                        request);

                if (updateResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (updateResponse.StatusCode ==
                    HttpStatusCode.Forbidden)
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                if (updateResponse.StatusCode ==
                    HttpStatusCode.BadRequest)
                {
                    ModelState.AddModelError(
                        "",
                        "The API rejected the profile details. " +
                        "Check the values and try again.");

                    return View(model);
                }

                if (!updateResponse.IsSuccessStatusCode)
                {
                    ModelState.AddModelError(
                        "",
                        "Unable to update your profile.");

                    return View(model);
                }

                HttpContext.Session.SetString(
                    "FirstName",
                    request.FirstName);

                TempData["SuccessMessage"] =
                    "Profile updated successfully!";

                return RedirectToAction(nameof(Index));
            }
            catch (HttpRequestException)
            {
                ModelState.AddModelError(
                    "",
                    "Unable to connect to RaceDay.API.");
            }
            catch (TaskCanceledException)
            {
                ModelState.AddModelError(
                    "",
                    "The request timed out. " +
                    "Check your profile before submitting again.");
            }
            catch (JsonException)
            {
                ModelState.AddModelError(
                    "",
                    "Unable to process profile information.");
            }

            return View(model);
        }

        // ==========================================
        // DISPLAY PROFILE PICTURE
        // GET /OrganiserProfile/Picture
        // ==========================================

        [HttpGet]
        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public async Task<IActionResult> Picture()
        {
            if (!HasApiToken())
                return Unauthorized();

            try
            {
                var client = CreateApiClient();

                using var response = await client.GetAsync(
                    "api/profile/picture",
                    HttpCompletionOption.ResponseHeadersRead);

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return Unauthorized();
                }

                if (response.StatusCode ==
                    HttpStatusCode.Forbidden)
                {
                    return Forbid();
                }

                if (response.StatusCode ==
                    HttpStatusCode.NotFound)
                {
                    return NotFound();
                }

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode(502);
                }

                const long maxImageSize = 5 * 1024 * 1024;

                if (response.Content.Headers.ContentLength
                    is long contentLength &&
                    contentLength > maxImageSize)
                {
                    return StatusCode(502);
                }

                var contentType = response.Content.Headers
                    .ContentType?.MediaType;

                if (contentType != "image/jpeg" &&
                    contentType != "image/png" &&
                    contentType != "image/webp")
                {
                    return StatusCode(502);
                }

                var imageBytes = await response.Content
                    .ReadAsByteArrayAsync();

                if (imageBytes.Length > maxImageSize)
                {
                    return StatusCode(502);
                }

                Response.Headers.CacheControl =
                    "private, no-store";

                return File(imageBytes, contentType);
            }
            catch (HttpRequestException)
            {
                return StatusCode(502);
            }
            catch (TaskCanceledException)
            {
                return StatusCode(504);
            }
        }

        // ==========================================
        // UPLOAD OR REPLACE PROFILE PICTURE
        // POST /OrganiserProfile/UploadPicture
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> UploadPicture(
            IFormFile? file)
        {
            if (!HasApiToken())
                return await LoginAgain();

            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] =
                    "Please select a profile picture.";

                return RedirectToAction(nameof(Index));
            }

            const long maxFileSize = 5 * 1024 * 1024;

            if (file.Length > maxFileSize)
            {
                TempData["ErrorMessage"] =
                    "The image must not exceed 5 MB.";

                return RedirectToAction(nameof(Index));
            }

            var extension = Path.GetExtension(
                file.FileName).ToLowerInvariant();

            if (extension != ".jpg" &&
                extension != ".jpeg" &&
                extension != ".png" &&
                extension != ".webp")
            {
                TempData["ErrorMessage"] =
                    "Only JPG, JPEG, PNG and WebP images are allowed.";

                return RedirectToAction(nameof(Index));
            }

            try
            {
                var client = CreateApiClient();

                using var formData =
                    new MultipartFormDataContent();

                using var fileStream =
                    file.OpenReadStream();

                using var fileContent =
                    new StreamContent(fileStream);

                fileContent.Headers.ContentType =
                    new MediaTypeHeaderValue(
                        extension == ".png"
                            ? "image/png"
                            : extension == ".webp"
                                ? "image/webp"
                                : "image/jpeg");

                // The API parameter is named "file".
                formData.Add(
                    fileContent,
                    "file",
                    Path.GetFileName(file.FileName));

                using var response = await client.PostAsync(
                    "api/profile/picture",
                    formData);

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (response.StatusCode ==
                    HttpStatusCode.Forbidden)
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                if (response.StatusCode ==
                    HttpStatusCode.BadRequest)
                {
                    TempData["ErrorMessage"] =
                        "The image was rejected. " +
                        "Please select a valid JPG, PNG or WebP image.";

                    return RedirectToAction(nameof(Index));
                }

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] =
                        "Profile picture uploaded successfully!";

                    return RedirectToAction(nameof(Index));
                }

                TempData["ErrorMessage"] =
                    "Unable to upload your profile picture.";
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] =
                    "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                TempData["ErrorMessage"] =
                    "The upload timed out. " +
                    "Check your profile before trying again.";
            }
            catch (IOException)
            {
                TempData["ErrorMessage"] =
                    "Unable to read the selected image.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // DELETE PROFILE PICTURE
        // POST /OrganiserProfile/DeletePicture
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePicture()
        {
            if (!HasApiToken())
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                // MVC uses POST with anti-forgery protection.
                // The API receives a DELETE request.

                using var response = await client.DeleteAsync(
                    "api/profile/picture");

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (response.StatusCode ==
                    HttpStatusCode.Forbidden)
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                if (response.StatusCode ==
                    HttpStatusCode.BadRequest ||
                    response.StatusCode ==
                    HttpStatusCode.NotFound)
                {
                    TempData["ErrorMessage"] =
                        "There is no profile picture to remove.";

                    return RedirectToAction(nameof(Index));
                }

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] =
                        "Profile picture removed successfully!";

                    return RedirectToAction(nameof(Index));
                }

                TempData["ErrorMessage"] =
                    "Unable to remove your profile picture.";
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] =
                    "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                TempData["ErrorMessage"] =
                    "The request timed out. " +
                    "Check your profile before trying again.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
