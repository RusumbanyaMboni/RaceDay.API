
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using RaceDay.MVC.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace RaceDay.MVC.Controllers
{
    [Authorize(Roles = "Organiser")]
    public class EventManagementController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public EventManagementController(
            IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // ==========================================
        // API CLIENT WITH JWT
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

        private int? GetOrganiserId()
        {
            var value = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            return int.TryParse(value, out var id)
                ? id
                : null;
        }

        private async Task<IActionResult> LoginAgain()
        {
            await HttpContext.SignOutAsync(
                "RaceDayCookie");

            HttpContext.Session.Clear();

            return RedirectToAction(
                "Login", "Account");
        }

        // ==========================================
        // MY EVENTS - GET
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var organiserId = GetOrganiserId();

            if (!HasApiToken() || organiserId == null)
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                using var response =
                    await client.GetAsync("api/Event");

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (!response.IsSuccessStatusCode)
                {
                    ViewBag.ErrorMessage =
                        "Unable to load events.";

                    return View(new List<EventViewModel>());
                }

                var events = await response.Content
                    .ReadFromJsonAsync<List<EventViewModel>>();

                var myEvents = (events ??
                    new List<EventViewModel>())
                    .Where(e =>
                        e.OrganiserId == organiserId.Value)
                    .OrderBy(e => e.EventDate)
                    .ToList();

                return View(myEvents);
            }
            catch (HttpRequestException)
            {
                ViewBag.ErrorMessage =
                    "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                ViewBag.ErrorMessage =
                    "The request timed out.";
            }
            catch (JsonException)
            {
                ViewBag.ErrorMessage =
                    "Unable to read event information.";
            }

            return View(new List<EventViewModel>());
        }

        // ==========================================
        // CREATE EVENT - GET
        // ==========================================

        [HttpGet]
        public IActionResult Create()
        {
            return View(new EventViewModel
            {
                EventDate = DateTime.Today.AddDays(1)
            });
        }

        // ==========================================
        // CREATE EVENT - POST
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            EventViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (!HasApiToken())
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                var eventData = new
                {
                    model.EventName,
                    model.Description,
                    model.EventDate,
                    model.Location,
                    model.Distance,
                    model.EventType,
                    OrganiserId = 0,
                    CreatedAt = DateTime.UtcNow
                };

                using var response =
                    await client.PostAsJsonAsync(
                        "api/Event", eventData);

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] =
                        "Event created successfully!";

                    return RedirectToAction("Index");
                }

                if (response.StatusCode ==
                    HttpStatusCode.Forbidden)
                {
                    ModelState.AddModelError(
                        "",
                        "The API denied permission to create this event.");
                }
                else if (response.StatusCode ==
                         HttpStatusCode.BadRequest)
                {
                    ModelState.AddModelError(
                        "",
                        "Invalid event information. Please check your details.");
                }
                else
                {
                    ModelState.AddModelError(
                        "",
                        "Unable to create event. API status: " +
                        (int)response.StatusCode);
                }
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
                    "The request timed out.");
            }
            catch (JsonException)
            {
                ModelState.AddModelError(
                    "",
                    "Unable to process API information.");
            }

            return View(model);
        }

        // ==========================================
        // EDIT EVENT - GET
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (id <= 0)
                return BadRequest();

            if (!HasApiToken())
                return await LoginAgain();

            var organiserId = GetOrganiserId();

            if (organiserId == null)
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                using var response =
                    await client.GetAsync(
                        $"api/Event/{id}");

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (response.StatusCode ==
                    HttpStatusCode.NotFound)
                {
                    return NotFound();
                }

                if (!response.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to load the selected event.";

                    return RedirectToAction("Index");
                }

                var eventItem = await response.Content
                    .ReadFromJsonAsync<EventViewModel>();

                if (eventItem == null)
                    return NotFound();

                if (eventItem.OrganiserId != organiserId.Value)
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                return View(eventItem);
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] =
                    "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                TempData["ErrorMessage"] =
                    "The request timed out.";
            }
            catch (JsonException)
            {
                TempData["ErrorMessage"] =
                    "Unable to read the event information.";
            }

            return RedirectToAction("Index");
        }

        // ==========================================
        // EDIT EVENT - POST
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            EventViewModel model)
        {
            if (id <= 0 || id != model.EventId)
                return BadRequest();

            if (!ModelState.IsValid)
                return View(model);

            if (!HasApiToken())
                return await LoginAgain();

            var organiserId = GetOrganiserId();

            if (organiserId == null)
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                using var getResponse =
                    await client.GetAsync(
                        $"api/Event/{id}");

                if (getResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (getResponse.StatusCode ==
                    HttpStatusCode.NotFound)
                {
                    return NotFound();
                }

                if (!getResponse.IsSuccessStatusCode)
                {
                    ModelState.AddModelError(
                        "",
                        "Unable to verify the selected event.");

                    return View(model);
                }

                var existingEvent =
                    await getResponse.Content
                        .ReadFromJsonAsync<EventViewModel>();

                if (existingEvent == null)
                    return NotFound();

                if (existingEvent.OrganiserId !=
                    organiserId.Value)
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                var eventData = new
                {
                    EventId = id,
                    OrganiserId = existingEvent.OrganiserId,
                    model.EventName,
                    model.Description,
                    model.EventDate,
                    model.Location,
                    model.Distance,
                    model.EventType,
                    CreatedAt = existingEvent.CreatedAt
                };

                using var response =
                    await client.PutAsJsonAsync(
                        $"api/Event/{id}",
                        eventData);

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (response.StatusCode ==
                    HttpStatusCode.Forbidden)
                {
                    ModelState.AddModelError(
                        "",
                        "You do not have permission to edit this event.");

                    return View(model);
                }

                if (response.StatusCode ==
                    HttpStatusCode.NotFound)
                {
                    return NotFound();
                }

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] =
                        "Event updated successfully!";

                    return RedirectToAction("Index");
                }

                if (response.StatusCode ==
                    HttpStatusCode.BadRequest)
                {
                    ModelState.AddModelError(
                        "",
                        "The API rejected the changes. Check the event details.");
                }
                else
                {
                    ModelState.AddModelError(
                        "",
                        "Unable to update event. API status: " +
                        (int)response.StatusCode);
                }
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
                    "The request timed out.");
            }
            catch (JsonException)
            {
                ModelState.AddModelError(
                    "",
                    "Unable to process event information.");
            }

            return View(model);
        }

        // ==========================================
        // DELETE EVENT - GET CONFIRMATION
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0)
                return BadRequest();

            if (!HasApiToken())
                return await LoginAgain();

            var organiserId = GetOrganiserId();

            if (organiserId == null)
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                using var response =
                    await client.GetAsync(
                        $"api/Event/{id}");

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (response.StatusCode ==
                    HttpStatusCode.NotFound)
                {
                    return NotFound();
                }

                if (!response.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to load the selected event.";

                    return RedirectToAction("Index");
                }

                var eventItem = await response.Content
                    .ReadFromJsonAsync<EventViewModel>();

                if (eventItem == null)
                    return NotFound();

                // Only the owning Organiser can
                // access the deletion confirmation.
                if (eventItem.OrganiserId != organiserId.Value)
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                return View(eventItem);
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] =
                    "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                TempData["ErrorMessage"] =
                    "The request timed out.";
            }
            catch (JsonException)
            {
                TempData["ErrorMessage"] =
                    "Unable to read the event information.";
            }

            return RedirectToAction("Index");
        }

        // ==========================================
        // DELETE EVENT - POST CONFIRMATION
        // ==========================================

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (id <= 0)
                return BadRequest();

            if (!HasApiToken())
                return await LoginAgain();

            var organiserId = GetOrganiserId();

            if (organiserId == null)
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                // Confirm current event ownership.
                using var getResponse =
                    await client.GetAsync(
                        $"api/Event/{id}");

                if (getResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (getResponse.StatusCode ==
                    HttpStatusCode.NotFound)
                {
                    TempData["ErrorMessage"] =
                        "This event no longer exists.";

                    return RedirectToAction("Index");
                }

                if (!getResponse.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to verify the selected event.";

                    return RedirectToAction("Index");
                }

                var eventItem = await getResponse.Content
                    .ReadFromJsonAsync<EventViewModel>();

                if (eventItem == null)
                    return NotFound();

                if (eventItem.OrganiserId != organiserId.Value)
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                // Send the deletion request to the API.
                using var response =
                    await client.DeleteAsync(
                        $"api/Event/{id}");

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (response.StatusCode ==
                    HttpStatusCode.Forbidden)
                {
                    TempData["ErrorMessage"] =
                        "You do not have permission to delete this event.";

                    return RedirectToAction("Index");
                }

                if (response.StatusCode ==
                    HttpStatusCode.NotFound)
                {
                    TempData["ErrorMessage"] =
                        "The event was not found.";

                    return RedirectToAction("Index");
                }

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] =
                        "Event deleted successfully!";

                    return RedirectToAction("Index");
                }

                TempData["ErrorMessage"] =
                    "Unable to delete event. API status: " +
                    (int)response.StatusCode;
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] =
                    "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                TempData["ErrorMessage"] =
                    "The delete request timed out. Refresh the event list to check whether the event was deleted.";
            }
            catch (JsonException)
            {
                TempData["ErrorMessage"] =
                    "Unable to process event information.";
            }

            return RedirectToAction("Index");
        }



        // ==========================================
        // EVENT DETAILS - GET
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (id <= 0)
                return BadRequest();

            if (!HasApiToken())
                return await LoginAgain();

            var organiserId = GetOrganiserId();

            if (organiserId == null)
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                using var response =
                    await client.GetAsync($"api/Event/{id}");

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
                    HttpStatusCode.NotFound)
                {
                    return NotFound();
                }

                if (!response.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to load event details.";

                    return RedirectToAction("Index");
                }

                var eventItem = await response.Content
                    .ReadFromJsonAsync<EventViewModel>();

                if (eventItem == null)
                    return NotFound();

                // Allow the Organiser to view their own event.
                if (eventItem.OrganiserId != organiserId.Value)
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                return View(eventItem);
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] =
                    "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                TempData["ErrorMessage"] =
                    "The request timed out.";
            }
            catch (JsonException)
            {
                TempData["ErrorMessage"] =
                    "Unable to read event details.";
            }

            return RedirectToAction("Index");
        }



        // ==========================================
        // EVENT IMAGE - DISPLAY
        // GET /EventManagement/Image/10
        // ==========================================
        [HttpGet]
        [ResponseCache(Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public async Task<IActionResult> Image(int id)
        {
            if (id <= 0) return BadRequest();
            if (!HasApiToken()) return Unauthorized();

            try
            {
                var client = CreateApiClient();
                using var response = await client.GetAsync(
                    $"api/Event/{id}/image",
                    HttpCompletionOption.ResponseHeadersRead);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    return Unauthorized();
                if (response.StatusCode == HttpStatusCode.Forbidden)
                    return Forbid();
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return NotFound();
                if (!response.IsSuccessStatusCode)
                    return StatusCode(502);

                const long maxBytes = 5 * 1024 * 1024;
                if (response.Content.Headers.ContentLength is long length
                    && length > maxBytes)
                    return StatusCode(502);

                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (contentType != "image/jpeg" &&
                    contentType != "image/png" &&
                    contentType != "image/webp")
                    return StatusCode(502);

                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (bytes.LongLength > maxBytes)
                    return StatusCode(502);

                Response.Headers.CacheControl = "private, no-store";
                return File(bytes, contentType);
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
        // EVENT IMAGE - UPLOAD OR REPLACE
        // POST /EventManagement/UploadImage/10
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> UploadImage(int id, IFormFile? file)
        {
            if (id <= 0) return BadRequest();
            if (!HasApiToken()) return await LoginAgain();

            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Please choose an event image.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "The image must be 5 MB or smaller.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".jpg" && extension != ".jpeg" &&
                extension != ".png" && extension != ".webp")
            {
                TempData["ErrorMessage"] = "Choose a JPG, PNG or WebP image.";
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                var client = CreateApiClient();
                using var form = new MultipartFormDataContent();
                using var stream = file.OpenReadStream();
                using var content = new StreamContent(stream);
                content.Headers.ContentType = new MediaTypeHeaderValue(
                    extension == ".png" ? "image/png" :
                    extension == ".webp" ? "image/webp" : "image/jpeg");
                form.Add(content, "file", Path.GetFileName(file.FileName));

                using var response = await client.PostAsync(
                    $"api/Event/{id}/image", form);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    return await LoginAgain();
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    TempData["ErrorMessage"] = "You do not own this event.";
                    return RedirectToAction(nameof(Index));
                }
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    TempData["ErrorMessage"] = "Event not found.";
                    return RedirectToAction(nameof(Index));
                }
                if (response.StatusCode == HttpStatusCode.BadRequest)
                    TempData["ErrorMessage"] = "The API rejected this image. Check the file format and size.";
                else if (response.IsSuccessStatusCode)
                    TempData["SuccessMessage"] = "Event image uploaded successfully!";
                else
                    TempData["ErrorMessage"] = "Unable to upload the event image.";
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] = "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                TempData["ErrorMessage"] = "The upload timed out. Check the event before retrying.";
            }
            catch (IOException)
            {
                TempData["ErrorMessage"] = "Unable to read the selected file.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // ==========================================
        // EVENT IMAGE - REMOVE
        // POST /EventManagement/DeleteImage/10
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int id)
        {
            if (id <= 0) return BadRequest();
            if (!HasApiToken()) return await LoginAgain();

            try
            {
                var client = CreateApiClient();
                using var response = await client.DeleteAsync(
                    $"api/Event/{id}/image");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    return await LoginAgain();
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    TempData["ErrorMessage"] = "You do not own this event.";
                    return RedirectToAction(nameof(Index));
                }
                if (response.StatusCode == HttpStatusCode.NotFound)
                    TempData["ErrorMessage"] = "The event or its image was not found.";
                else if (response.IsSuccessStatusCode)
                    TempData["SuccessMessage"] = "Event image removed successfully!";
                else
                    TempData["ErrorMessage"] = "Unable to remove the event image.";
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] = "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                TempData["ErrorMessage"] = "The request timed out. Check the event before retrying.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

    }
}
