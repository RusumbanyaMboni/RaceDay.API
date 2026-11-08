
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.MVC.Models;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace RaceDay.MVC.Controllers
{
    [Authorize(Roles = "Organiser")]
    public class OrganiserResultsController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public OrganiserResultsController(
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

            var token = HttpContext.Session.GetString("JwtToken");

            if (!string.IsNullOrWhiteSpace(token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
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

        private IActionResult AccessDenied()
        {
            return RedirectToAction("AccessDenied", "Account");
        }

        private async Task<bool> OwnsEventAsync(
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

        private static bool TryParseFinishTime(
            string? value,
            out TimeSpan finishTime)
        {
            return TimeSpan.TryParseExact(
                value,
                @"hh\:mm\:ss",
                CultureInfo.InvariantCulture,
                out finishTime)
                && finishTime > TimeSpan.Zero;
        }

        private static string FormatFinishTime(TimeSpan time)
        {
            return time.ToString(@"hh\:mm\:ss");
        }

        // ==========================================
        // VIEW EVENT RESULTS
        // GET /OrganiserResults/Index?eventId=10
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Index(int eventId)
        {
            if (eventId <= 0)
                return BadRequest();

            if (!HasApiToken())
                return await LoginAgain();

            var organiserId = GetOrganiserId();

            if (organiserId == null)
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                using var eventResponse = await client.GetAsync(
                    $"api/Event/{eventId}");

                if (eventResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (eventResponse.StatusCode ==
                    HttpStatusCode.Forbidden)
                    return AccessDenied();

                if (eventResponse.StatusCode ==
                    HttpStatusCode.NotFound)
                    return NotFound();

                if (!eventResponse.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to load the selected event.";

                    return RedirectToAction(
                        "Index", "EventManagement");
                }

                var eventItem = await eventResponse.Content
                    .ReadFromJsonAsync<EventViewModel>();

                if (eventItem == null)
                    return NotFound();

                if (eventItem.OrganiserId != organiserId.Value)
                    return AccessDenied();

                using var resultsResponse = await client.GetAsync(
                    $"api/events/{eventId}/results");

                if (resultsResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (!resultsResponse.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to load race results.";

                    return RedirectToAction(
                        "Details",
                        "EventManagement",
                        new { id = eventId });
                }

                var results = await resultsResponse.Content
                    .ReadFromJsonAsync<List<ResultViewModel>>();

                ViewBag.EventId = eventId;
                ViewBag.EventName = eventItem.EventName;

                return View(
                    (results ?? new List<ResultViewModel>())
                    .OrderBy(r => r.FinishPosition)
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
                    "Unable to process race results.";
            }

            return RedirectToAction(
                "Details",
                "EventManagement",
                new { id = eventId });
        }

        // ==========================================
        // RECORD RESULT - GET
        // GET /OrganiserResults/Record?enrolmentId=16
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Record(int enrolmentId)
        {
            if (enrolmentId <= 0)
                return BadRequest();

            if (!HasApiToken())
                return await LoginAgain();

            var organiserId = GetOrganiserId();

            if (organiserId == null)
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                using var response = await client.GetAsync(
                    $"api/enrolments/{enrolmentId}");

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (response.StatusCode ==
                    HttpStatusCode.Forbidden)
                    return AccessDenied();

                if (response.StatusCode ==
                    HttpStatusCode.NotFound)
                    return NotFound();

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

                if (!await OwnsEventAsync(
                    client,
                    enrolment.EventId,
                    organiserId.Value))
                    return AccessDenied();

                var model = new RecordResultViewModel
                {
                    EventId = enrolment.EventId,
                    EnrolmentId = enrolment.EnrolmentId,
                    ParticipantId = enrolment.ParticipantId,
                    EventName = enrolment.EventName ?? "",
                    CategoryName = enrolment.CategoryName ?? "",
                    FinishTime = "",
                    FinishPosition = 0
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
                    "Unable to read enrolment information.";
            }

            return RedirectToAction(
                "Index", "EventManagement");
        }

        // ==========================================
        // RECORD RESULT - POST
        // POST /OrganiserResults/Record
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Record(
            int enrolmentId,
            RecordResultViewModel model)
        {
            if (enrolmentId <= 0)
                return BadRequest();

            if (!HasApiToken())
                return await LoginAgain();

            var organiserId = GetOrganiserId();

            if (organiserId == null)
                return await LoginAgain();

            if (!TryParseFinishTime(
                model.FinishTime,
                out var finishTime))
            {
                ModelState.AddModelError(
                    nameof(model.FinishTime),
                    "Enter a finish time in HH:MM:SS format, greater than zero.");
            }

            if (model.FinishPosition <= 0)
            {
                ModelState.AddModelError(
                    nameof(model.FinishPosition),
                    "Finish position must be greater than zero.");
            }

            try
            {
                var client = CreateApiClient();

                using var enrolmentResponse = await client.GetAsync(
                    $"api/enrolments/{enrolmentId}");

                if (enrolmentResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (enrolmentResponse.StatusCode ==
                    HttpStatusCode.Forbidden)
                    return AccessDenied();

                if (enrolmentResponse.StatusCode ==
                    HttpStatusCode.NotFound)
                    return NotFound();

                if (!enrolmentResponse.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to verify the enrolment.";

                    return RedirectToAction(
                        "Index", "EventManagement");
                }

                var enrolment = await enrolmentResponse.Content
                    .ReadFromJsonAsync<EnrolmentViewModel>();

                if (enrolment == null)
                    return NotFound();

                if (!await OwnsEventAsync(
                    client,
                    enrolment.EventId,
                    organiserId.Value))
                    return AccessDenied();

                model.EventId = enrolment.EventId;
                model.EnrolmentId = enrolment.EnrolmentId;
                model.ParticipantId = enrolment.ParticipantId;
                model.EventName = enrolment.EventName ?? "";
                model.CategoryName = enrolment.CategoryName ?? "";

                if (!ModelState.IsValid)
                    return View(model);

                var request = new
                {
                    FinishTime = finishTime,
                    FinishPosition = model.FinishPosition
                };

                using var resultResponse = await client.PostAsJsonAsync(
                    $"api/enrolments/{enrolmentId}/results",
                    request);

                if (resultResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (resultResponse.StatusCode ==
                    HttpStatusCode.Forbidden)
                    return AccessDenied();

                if (resultResponse.StatusCode ==
                    HttpStatusCode.Conflict)
                {
                    ModelState.AddModelError(
                        "",
                        "A result already exists for this enrolment.");

                    return View(model);
                }

                if (!resultResponse.IsSuccessStatusCode)
                {
                    ModelState.AddModelError(
                        "",
                        "Unable to record the race result.");

                    return View(model);
                }

                TempData["SuccessMessage"] =
                    "Race result recorded successfully!";

                return RedirectToAction(
                    "Index",
                    new { eventId = enrolment.EventId });
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
                    "The request timed out. Check existing results before retrying.");
            }
            catch (JsonException)
            {
                ModelState.AddModelError(
                    "",
                    "Unable to process enrolment information.");
            }

            return View(model);
        }

        // ==========================================
        // EDIT RESULT - GET
        // GET /OrganiserResults/Edit/1
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

                using var response = await client.GetAsync(
                    $"api/results/{id}");

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (response.StatusCode ==
                    HttpStatusCode.NotFound)
                    return NotFound();

                if (!response.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to load the selected result.";

                    return RedirectToAction(
                        "Index", "EventManagement");
                }

                var result = await response.Content
                    .ReadFromJsonAsync<ResultViewModel>();

                if (result == null)
                    return NotFound();

                if (!await OwnsEventAsync(
                    client,
                    result.EventId,
                    organiserId.Value))
                    return AccessDenied();

                var model = new EditResultViewModel
                {
                    ResultId = result.ResultId,
                    EnrolmentId = result.EnrolmentId,
                    EventId = result.EventId,
                    EventName = result.EventName ?? "",
                    ParticipantName = result.ParticipantName ?? "",
                    CategoryName = result.CategoryName ?? "",
                    FinishTime = FormatFinishTime(result.FinishTime),
                    FinishPosition = result.FinishPosition
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
                    "Unable to read result information.";
            }

            return RedirectToAction(
                "Index", "EventManagement");
        }

        // ==========================================
        // EDIT RESULT - POST
        // PUT /api/results/{id} through MVC
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            EditResultViewModel model)
        {
            if (id <= 0)
                return BadRequest();

            if (!HasApiToken())
                return await LoginAgain();

            var organiserId = GetOrganiserId();

            if (organiserId == null)
                return await LoginAgain();

            if (!TryParseFinishTime(
                model.FinishTime,
                out var finishTime))
            {
                ModelState.AddModelError(
                    nameof(model.FinishTime),
                    "Enter a valid time in HH:MM:SS format.");
            }

            if (model.FinishPosition <= 0)
            {
                ModelState.AddModelError(
                    nameof(model.FinishPosition),
                    "Finish position must be greater than zero.");
            }

            try
            {
                var client = CreateApiClient();

                // Reload the result to verify the
                // associated event and ownership.

                using var response = await client.GetAsync(
                    $"api/results/{id}");

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (response.StatusCode ==
                    HttpStatusCode.NotFound)
                    return NotFound();

                if (!response.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to verify the selected result.";

                    return RedirectToAction(
                        "Index", "EventManagement");
                }

                var existingResult = await response.Content
                    .ReadFromJsonAsync<ResultViewModel>();

                if (existingResult == null)
                    return NotFound();

                if (!await OwnsEventAsync(
                    client,
                    existingResult.EventId,
                    organiserId.Value))
                    return AccessDenied();

                // Use trusted API data, not browser-supplied IDs.
                model.ResultId = existingResult.ResultId;
                model.EventId = existingResult.EventId;
                model.EnrolmentId = existingResult.EnrolmentId;
                model.EventName = existingResult.EventName ?? "";
                model.ParticipantName =
                    existingResult.ParticipantName ?? "";
                model.CategoryName =
                    existingResult.CategoryName ?? "";

                if (!ModelState.IsValid)
                    return View(model);

                var request = new
                {
                    FinishTime = finishTime,
                    FinishPosition = model.FinishPosition
                };

                using var updateResponse = await client.PutAsJsonAsync(
                    $"api/results/{id}",
                    request);

                if (updateResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (updateResponse.StatusCode ==
                    HttpStatusCode.Forbidden)
                    return AccessDenied();

                if (updateResponse.StatusCode ==
                    HttpStatusCode.NotFound)
                    return NotFound();

                if (!updateResponse.IsSuccessStatusCode)
                {
                    ModelState.AddModelError(
                        "",
                        "Unable to update the race result.");

                    return View(model);
                }

                TempData["SuccessMessage"] =
                    "Race result updated successfully!";

                return RedirectToAction(
                    "Index",
                    new { eventId = existingResult.EventId });
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
                    "The request timed out. Check the result before retrying.");
            }
            catch (JsonException)
            {
                ModelState.AddModelError(
                    "",
                    "Unable to process result information.");
            }

            return View(model);
        }

        // ==========================================
        // DELETE RESULT - GET
        // GET /OrganiserResults/Delete/1
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

                using var response = await client.GetAsync(
                    $"api/results/{id}");

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (response.StatusCode ==
                    HttpStatusCode.NotFound)
                    return NotFound();

                if (!response.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to load the selected result.";

                    return RedirectToAction(
                        "Index", "EventManagement");
                }

                var result = await response.Content
                    .ReadFromJsonAsync<ResultViewModel>();

                if (result == null)
                    return NotFound();

                if (!await OwnsEventAsync(
                    client,
                    result.EventId,
                    organiserId.Value))
                    return AccessDenied();

                return View(result);
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
                    "Unable to read result information.";
            }

            return RedirectToAction(
                "Index", "EventManagement");
        }

        // ==========================================
        // DELETE RESULT - POST
        // DELETE /api/results/{id} through MVC
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

                // Reload the result before deletion.

                using var getResponse = await client.GetAsync(
                    $"api/results/{id}");

                if (getResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (getResponse.StatusCode ==
                    HttpStatusCode.NotFound)
                {
                    TempData["ErrorMessage"] =
                        "The selected result no longer exists.";

                    return RedirectToAction(
                        "Index", "EventManagement");
                }

                if (!getResponse.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to verify the selected result.";

                    return RedirectToAction(
                        "Index", "EventManagement");
                }

                var result = await getResponse.Content
                    .ReadFromJsonAsync<ResultViewModel>();

                if (result == null)
                    return NotFound();

                if (!await OwnsEventAsync(
                    client,
                    result.EventId,
                    organiserId.Value))
                    return AccessDenied();

                using var deleteResponse = await client.DeleteAsync(
                    $"api/results/{id}");

                if (deleteResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (deleteResponse.StatusCode ==
                    HttpStatusCode.Forbidden)
                    return AccessDenied();

                if (deleteResponse.StatusCode ==
                    HttpStatusCode.NotFound)
                {
                    TempData["ErrorMessage"] =
                        "This result has already been deleted.";

                    return RedirectToAction(
                        "Index",
                        new { eventId = result.EventId });
                }

                if (!deleteResponse.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to delete the race result.";

                    return RedirectToAction(
                        "Index",
                        new { eventId = result.EventId });
                }

                TempData["SuccessMessage"] =
                    "Race result deleted successfully!";

                return RedirectToAction(
                    "Index",
                    new { eventId = result.EventId });
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] =
                    "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                TempData["ErrorMessage"] =
                    "The request timed out. Check whether the result still exists.";
            }
            catch (JsonException)
            {
                TempData["ErrorMessage"] =
                    "Unable to process result information.";
            }

            return RedirectToAction(
                "Index", "EventManagement");
        }
    }
}
