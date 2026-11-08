
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.MVC.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.MVC.Controllers
{
    [Authorize(Roles = "Participant")]
    public class ParticipantEventsController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public ParticipantEventsController(
            IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // ==========================================
        // CREATE API CLIENT
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

        // ==========================================
        // LOGIN AGAIN
        // ==========================================

        private async Task<IActionResult> LoginAgain()
        {
            await HttpContext.SignOutAsync("RaceDayCookie");
            HttpContext.Session.Clear();

            return RedirectToAction("Login", "Account");
        }

        // ==========================================
        // BROWSE UPCOMING EVENTS
        // GET /ParticipantEvents/Index
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Index(
            string? search,
            string? eventType)
        {
            if (string.IsNullOrWhiteSpace(
                HttpContext.Session.GetString("JwtToken")))
            {
                return await LoginAgain();
            }

            try
            {
                var client = CreateApiClient();

                using var response = await client.GetAsync(
                    "api/Event");

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
                    ViewBag.ErrorMessage =
                        "Unable to load events.";

                    return View(new List<EventViewModel>());
                }

                var events = await response.Content
                    .ReadFromJsonAsync<List<EventViewModel>>();

                var upcomingEvents = (events ??
                    new List<EventViewModel>())
                    .Where(e => e.EventDate >= DateTime.Today);

                // Filter by event name or location.
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var keyword = search.Trim();

                    upcomingEvents = upcomingEvents.Where(e =>
                        e.EventName.Contains(
                            keyword,
                            StringComparison.OrdinalIgnoreCase)
                        ||
                        e.Location.Contains(
                            keyword,
                            StringComparison.OrdinalIgnoreCase));
                }

                // Filter by Run, Walk or Cycle.
                if (!string.IsNullOrWhiteSpace(eventType))
                {
                    upcomingEvents = upcomingEvents.Where(e =>
                        string.Equals(
                            e.EventType,
                            eventType,
                            StringComparison.OrdinalIgnoreCase));
                }

                ViewBag.Search = search;
                ViewBag.EventType = eventType;

                return View(upcomingEvents
                    .OrderBy(e => e.EventDate)
                    .ToList());
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
                    "Unable to process event information.";
            }

            return View(new List<EventViewModel>());
        }


        // ==========================================
        // PARTICIPANT EVENT DETAILS
        // GET /ParticipantEvents/Details/{id}
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (id <= 0)
                return BadRequest();

            if (string.IsNullOrWhiteSpace(
                HttpContext.Session.GetString("JwtToken")))
            {
                return await LoginAgain();
            }

            try
            {
                var client = CreateApiClient();

                // Get event information.
                using var eventResponse = await client.GetAsync(
                    $"api/Event/{id}");

                if (eventResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (eventResponse.StatusCode ==
                    HttpStatusCode.NotFound)
                {
                    return NotFound();
                }

                if (!eventResponse.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to load the selected event.";

                    return RedirectToAction("Index");
                }

                var eventItem = await eventResponse.Content
                    .ReadFromJsonAsync<EventViewModel>();

                if (eventItem == null)
                    return NotFound();

                // Load categories for this event.
                using var categoryResponse = await client.GetAsync(
                    $"api/events/{id}/categories");

                if (categoryResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (!categoryResponse.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to load event categories.";

                    return RedirectToAction("Index");
                }

                var categories = await categoryResponse.Content
                    .ReadFromJsonAsync<List<CategoryViewModel>>();

                var model = new ParticipantEventDetailsViewModel
                {
                    Event = eventItem,
                    Categories = categories ??
                        new List<CategoryViewModel>()
                };

                return View(model);
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
                    "Unable to process event information.";
            }

            return RedirectToAction("Index");
        }


        // ==========================================
        // PARTICIPANT EVENT REGISTRATION
        // POST /ParticipantEvents/Enrol
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enrol(
            int eventId,
            int selectedCategoryId)
        {
            if (eventId <= 0 || selectedCategoryId <= 0)
            {
                TempData["ErrorMessage"] =
                    "Please select a valid category.";

                return RedirectToAction(
                    "Details", new { id = eventId });
            }

            if (string.IsNullOrWhiteSpace(
                HttpContext.Session.GetString("JwtToken")))
            {
                return await LoginAgain();
            }

            try
            {
                var client = CreateApiClient();

                var request = new
                {
                    CategoryId = selectedCategoryId
                };

                using var response = await client.PostAsJsonAsync(
                    $"api/events/{eventId}/enrolments",
                    request);

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
                    HttpStatusCode.Conflict)
                {
                    TempData["ErrorMessage"] =
                        "You are already enrolled in this event.";

                    return RedirectToAction(
                        "Details", new { id = eventId });
                }

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] =
                        "You have successfully enrolled in the event!";

                    return RedirectToAction(
                        "Details", new { id = eventId });
                }

                TempData["ErrorMessage"] =
                    "Registration failed. Please check the selected category.";
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] =
                    "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                TempData["ErrorMessage"] =
                    "The registration request timed out. Please check your enrolment before retrying.";
            }

            return RedirectToAction(
                "Details", new { id = eventId });
        }



        // ==========================================
        // VIEW EVENT IMAGE (PRIVATE AZURE BLOB)
        // GET /ParticipantEvents/Image/{id}
        // ==========================================

        [HttpGet]
        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public async Task<IActionResult> Image(int id)
        {
            if (id <= 0)
                return BadRequest();

            // Image requests should return HTTP status codes,
            // not login-page HTML inside an image element.
            if (string.IsNullOrWhiteSpace(
                HttpContext.Session.GetString("JwtToken")))
            {
                return Unauthorized();
            }

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

                const long maxImageSize = 5 * 1024 * 1024;

                if (response.Content.Headers.ContentLength is long length
                    && length > maxImageSize)
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

                // Bound the download even when Content-Length is absent.
                await using var responseStream =
                    await response.Content.ReadAsStreamAsync();
                await using var output = new MemoryStream();
                var buffer = new byte[81920];
                int bytesRead;

                while ((bytesRead = await responseStream.ReadAsync(buffer)) > 0)
                {
                    if (output.Length + bytesRead > maxImageSize)
                        return StatusCode(502);

                    await output.WriteAsync(buffer.AsMemory(0, bytesRead));
                }

                Response.Headers.CacheControl = "private, no-store";

                return File(output.ToArray(), contentType);
            }
            catch (HttpRequestException)
            {
                return StatusCode(502);
            }
            catch (TaskCanceledException)
            {
                return StatusCode(504);
            }
            catch (IOException)
            {
                return StatusCode(502);
            }
        }

    }
}
