
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.MVC.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace RaceDay.MVC.Controllers
{
    [Authorize(Roles = "Organiser")]
    public class OrganiserEnrolmentsController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        private static readonly string[] AllowedStatuses =
        {
            "Registered",
            "Confirmed",
            "Pending",
            "Rejected"
        };

        public OrganiserEnrolmentsController(
            IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // ==========================================
        // HELPERS
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
            await HttpContext.SignOutAsync("RaceDayCookie");

            HttpContext.Session.Clear();

            return RedirectToAction("Login", "Account");
        }

        // ==========================================
        // VERIFY EVENT OWNERSHIP
        // ==========================================

        private async Task<bool> OwnsEvent(
            HttpClient client,
            int eventId,
            int organiserId)
        {
            using var response = await client.GetAsync(
                $"api/Event/{eventId}");

            if (!response.IsSuccessStatusCode)
                return false;

            var eventItem = await response.Content
                .ReadFromJsonAsync<EventViewModel>();

            return eventItem != null &&
                   eventItem.OrganiserId == organiserId;
        }

        // ==========================================
        // VIEW EVENT ENROLMENTS - GET
        // GET /OrganiserEnrolments/Index?eventId=10
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Index(int eventId)
        {
            if (eventId <= 0)
                return BadRequest();

            if (!HasApiToken())
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                using var eventResponse = await client.GetAsync(
                    $"api/Event/{eventId}");

                if (eventResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (eventResponse.StatusCode ==
                    HttpStatusCode.Forbidden)
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
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

                    return RedirectToAction(
                        "Index", "EventManagement");
                }

                var eventItem = await eventResponse.Content
                    .ReadFromJsonAsync<EventViewModel>();

                var organiserId = GetOrganiserId();

                if (eventItem == null ||
                    organiserId == null ||
                    eventItem.OrganiserId != organiserId.Value)
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                using var response = await client.GetAsync(
                    $"api/events/{eventId}/enrolments");

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
                        "Unable to load event enrolments.";

                    return RedirectToAction(
                        "Details",
                        "EventManagement",
                        new { id = eventId });
                }

                var enrolments = await response.Content
                    .ReadFromJsonAsync<
                        List<OrganiserEnrolmentViewModel>>();

                ViewBag.EventId = eventId;
                ViewBag.EventName = eventItem.EventName;

                return View(
                    (enrolments ??
                        new List<OrganiserEnrolmentViewModel>())
                    .OrderByDescending(e => e.EnrolmentDate)
                    .ToList());
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
                    "Unable to process enrolment information.";
            }

            return RedirectToAction(
                "Details",
                "EventManagement",
                new { id = eventId });
        }

        // ==========================================
        // UPDATE STATUS - GET
        // GET /OrganiserEnrolments/UpdateStatus/1
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> UpdateStatus(int id)
        {
            if (id <= 0)
                return BadRequest();

            if (!HasApiToken())
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                using var response = await client.GetAsync(
                    $"api/enrolments/{id}");

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
                        "Unable to load the selected enrolment.";

                    return RedirectToAction(
                        "Index", "EventManagement");
                }

                var enrolment = await response.Content
                    .ReadFromJsonAsync<EnrolmentViewModel>();

                if (enrolment == null)
                    return NotFound();

                var organiserId = GetOrganiserId();

                if (organiserId == null)
                    return await LoginAgain();

                if (!await OwnsEvent(
                    client,
                    enrolment.EventId,
                    organiserId.Value))
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                var model = new UpdateEnrolmentStatusViewModel
                {
                    EnrolmentId = enrolment.EnrolmentId,
                    EventId = enrolment.EventId,
                    ParticipantId = enrolment.ParticipantId,
                    EventName = enrolment.EventName,
                    CategoryName = enrolment.CategoryName,
                    CurrentStatus = enrolment.Status,
                    NewStatus = enrolment.Status
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
                    "Unable to process enrolment information.";
            }

            return RedirectToAction(
                "Index", "EventManagement");
        }

        // ==========================================
        // UPDATE STATUS - POST
        // POST /OrganiserEnrolments/UpdateStatus/1
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(
            int id,
            string newStatus)
        {
            if (id <= 0)
                return BadRequest();

            if (!HasApiToken())
                return await LoginAgain();

            var selectedStatus = AllowedStatuses
                .FirstOrDefault(status =>
                    string.Equals(
                        status,
                        newStatus,
                        StringComparison.OrdinalIgnoreCase));

            if (selectedStatus == null)
            {
                TempData["ErrorMessage"] =
                    "Please select a valid enrolment status.";

                return RedirectToAction(
                    "UpdateStatus", new { id });
            }

            try
            {
                var client = CreateApiClient();

                // Load the enrolment again to ensure
                // it still exists and to identify its event.

                using var enrolmentResponse = await client.GetAsync(
                    $"api/enrolments/{id}");

                if (enrolmentResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (enrolmentResponse.StatusCode ==
                    HttpStatusCode.Forbidden)
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                if (enrolmentResponse.StatusCode ==
                    HttpStatusCode.NotFound)
                {
                    return NotFound();
                }

                if (!enrolmentResponse.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to verify the selected enrolment.";

                    return RedirectToAction(
                        "Index", "EventManagement");
                }

                var enrolment = await enrolmentResponse.Content
                    .ReadFromJsonAsync<EnrolmentViewModel>();

                if (enrolment == null)
                    return NotFound();

                var organiserId = GetOrganiserId();

                if (organiserId == null)
                    return await LoginAgain();

                if (!await OwnsEvent(
                    client,
                    enrolment.EventId,
                    organiserId.Value))
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                if (string.Equals(
                    enrolment.Status,
                    selectedStatus,
                    StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] =
                        "This enrolment already has the selected status.";

                    return RedirectToAction(
                        "UpdateStatus", new { id });
                }

                // The API allows an Organiser
                // to update the enrolment status.

                var request = new
                {
                    Status = selectedStatus
                };

                using var updateResponse = await client.PutAsJsonAsync(
                    $"api/enrolments/{id}",
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
                    HttpStatusCode.NotFound)
                {
                    TempData["ErrorMessage"] =
                        "The selected enrolment no longer exists.";

                    return RedirectToAction(
                        "Index",
                        new { eventId = enrolment.EventId });
                }

                if (updateResponse.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] =
                        "Enrolment status updated successfully!";

                    return RedirectToAction(
                        "Index",
                        new { eventId = enrolment.EventId });
                }

                TempData["ErrorMessage"] =
                    "Unable to update enrolment status.";

                return RedirectToAction(
                    "UpdateStatus", new { id });
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] =
                    "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                TempData["ErrorMessage"] =
                    "The request timed out. Check the enrolment status before retrying.";
            }
            catch (JsonException)
            {
                TempData["ErrorMessage"] =
                    "Unable to process enrolment information.";
            }

            return RedirectToAction(
                "Index", "EventManagement");
        }


        // ==========================================
        // REMOVE ENROLMENT - GET
        // GET /OrganiserEnrolments/Remove/{id}
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Remove(int id)
        {
            if (id <= 0)
                return BadRequest();

            if (!HasApiToken())
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                // Retrieve enrolment information.
                // RaceDay.API verifies ownership.

                using var response = await client.GetAsync(
                    $"api/enrolments/{id}");

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
                        "Unable to load the selected enrolment.";

                    return RedirectToAction(
                        "Index", "EventManagement");
                }

                var enrolment = await response.Content
                    .ReadFromJsonAsync<EnrolmentViewModel>();

                if (enrolment == null)
                    return NotFound();

                var organiserId = GetOrganiserId();

                if (organiserId == null)
                    return await LoginAgain();

                // Confirm that the current Organiser
                // owns the enrolment's event.

                if (!await OwnsEvent(
                    client,
                    enrolment.EventId,
                    organiserId.Value))
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                return View(enrolment);
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
                    "Unable to read enrolment information.";
            }

            return RedirectToAction(
                "Index", "EventManagement");
        }

        // ==========================================
        // REMOVE ENROLMENT - POST
        // POST /OrganiserEnrolments/Remove/{id}
        // ==========================================

        [HttpPost]
        [ActionName("Remove")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveConfirmed(int id)
        {
            if (id <= 0)
                return BadRequest();

            if (!HasApiToken())
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                // Retrieve the enrolment again.
                // Do not trust an EventId submitted
                // by the browser.

                using var getResponse = await client.GetAsync(
                    $"api/enrolments/{id}");

                if (getResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (getResponse.StatusCode ==
                    HttpStatusCode.Forbidden)
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                if (getResponse.StatusCode ==
                    HttpStatusCode.NotFound)
                {
                    TempData["ErrorMessage"] =
                        "This enrolment no longer exists.";

                    return RedirectToAction(
                        "Index", "EventManagement");
                }

                if (!getResponse.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to verify the selected enrolment.";

                    return RedirectToAction(
                        "Index", "EventManagement");
                }

                var enrolment = await getResponse.Content
                    .ReadFromJsonAsync<EnrolmentViewModel>();

                if (enrolment == null)
                    return NotFound();

                var organiserId = GetOrganiserId();

                if (organiserId == null)
                    return await LoginAgain();

                // Verify event ownership again
                // immediately before deletion.

                if (!await OwnsEvent(
                    client,
                    enrolment.EventId,
                    organiserId.Value))
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                // Delete through the API.
                // The API performs its own ownership check.

                using var deleteResponse = await client.DeleteAsync(
                    $"api/enrolments/{id}");

                if (deleteResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (deleteResponse.StatusCode ==
                    HttpStatusCode.Forbidden)
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                if (deleteResponse.StatusCode ==
                    HttpStatusCode.NotFound)
                {
                    TempData["ErrorMessage"] =
                        "The enrolment has already been removed.";

                    return RedirectToAction(
                        "Index",
                        new { eventId = enrolment.EventId });
                }

                if (deleteResponse.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] =
                        "Participant enrolment removed successfully!";

                    return RedirectToAction(
                        "Index",
                        new { eventId = enrolment.EventId });
                }

                TempData["ErrorMessage"] =
                    "Unable to remove the enrolment. Please try again.";

                return RedirectToAction(
                    "Index",
                    new { eventId = enrolment.EventId });
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] =
                    "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                TempData["ErrorMessage"] =
                    "The request timed out. Check whether the enrolment still exists before retrying.";
            }
            catch (JsonException)
            {
                TempData["ErrorMessage"] =
                    "Unable to process enrolment information.";
            }

            return RedirectToAction(
                "Index", "EventManagement");
        }


    }
}
