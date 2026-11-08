
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
    public class ParticipantEnrolmentsController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public ParticipantEnrolmentsController(
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
                    new AuthenticationHeaderValue("Bearer", token);
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
        // MY ENROLMENTS - GET
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
                    "api/enrolments/my-enrolments");

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
                        "Unable to load your enrolments.";

                    return View(new List<EnrolmentViewModel>());
                }

                var enrolments = await response.Content
                    .ReadFromJsonAsync<List<EnrolmentViewModel>>();

                return View(
                    (enrolments ?? new List<EnrolmentViewModel>())
                    .OrderByDescending(e => e.EnrolmentDate)
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
                    "Unable to process enrolment information.";
            }

            return View(new List<EnrolmentViewModel>());
        }

        // ==========================================
        // CHANGE CATEGORY - GET
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> ChangeCategory(int id)
        {
            if (id <= 0)
                return BadRequest();

            if (!HasApiToken())
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

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
                        "Unable to load the selected enrolment.";

                    return RedirectToAction("Index");
                }

                var enrolment = await enrolmentResponse.Content
                    .ReadFromJsonAsync<EnrolmentViewModel>();

                if (enrolment == null)
                    return NotFound();

                using var categoriesResponse = await client.GetAsync(
                    $"api/events/{enrolment.EventId}/categories");

                if (categoriesResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (!categoriesResponse.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to load available categories.";

                    return RedirectToAction("Index");
                }

                var categories = await categoriesResponse.Content
                    .ReadFromJsonAsync<List<CategoryViewModel>>();

                var model = new ChangeCategoryViewModel
                {
                    EnrolmentId = enrolment.EnrolmentId,
                    EventId = enrolment.EventId,
                    EventName = enrolment.EventName ?? "Event",
                    CurrentCategoryId = enrolment.CategoryId,
                    CurrentCategoryName =
                        enrolment.CategoryName ?? "Unknown",
                    SelectedCategoryId = enrolment.CategoryId,
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
                    "Unable to read category information.";
            }

            return RedirectToAction("Index");
        }

        // ==========================================
        // CHANGE CATEGORY - POST
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeCategory(
            int id,
            int selectedCategoryId)
        {
            if (id <= 0)
                return BadRequest();

            if (selectedCategoryId <= 0)
            {
                TempData["ErrorMessage"] =
                    "Please select a valid category.";

                return RedirectToAction(
                    "ChangeCategory", new { id });
            }

            if (!HasApiToken())
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

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
                        "Unable to verify your enrolment.";

                    return RedirectToAction("Index");
                }

                var enrolment = await enrolmentResponse.Content
                    .ReadFromJsonAsync<EnrolmentViewModel>();

                if (enrolment == null)
                    return NotFound();

                using var categoryResponse = await client.GetAsync(
                    $"api/categories/{selectedCategoryId}");

                if (categoryResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (!categoryResponse.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "The selected category is not available.";

                    return RedirectToAction(
                        "ChangeCategory", new { id });
                }

                var category = await categoryResponse.Content
                    .ReadFromJsonAsync<CategoryViewModel>();

                if (category == null ||
                    category.EventId != enrolment.EventId)
                {
                    TempData["ErrorMessage"] =
                        "The selected category does not belong to this event.";

                    return RedirectToAction(
                        "ChangeCategory", new { id });
                }

                if (category.CategoryId == enrolment.CategoryId)
                {
                    TempData["ErrorMessage"] =
                        "You are already registered in this category.";

                    return RedirectToAction(
                        "ChangeCategory", new { id });
                }

                var request = new
                {
                    CategoryId = selectedCategoryId
                };

                using var updateResponse = await client.PutAsJsonAsync(
                    $"api/enrolments/{id}", request);

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

                if (updateResponse.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] =
                        "Your enrolment category has been updated successfully!";

                    return RedirectToAction("Index");
                }

                TempData["ErrorMessage"] =
                    "Unable to update your category.";

                return RedirectToAction(
                    "ChangeCategory", new { id });
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] =
                    "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                TempData["ErrorMessage"] =
                    "The request timed out. Check your enrolment before retrying.";
            }
            catch (JsonException)
            {
                TempData["ErrorMessage"] =
                    "Unable to process category information.";
            }

            return RedirectToAction("Index");
        }

        // ==========================================
        // CANCEL ENROLMENT - GET
        // Displays confirmation page
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Cancel(int id)
        {
            if (id <= 0)
                return BadRequest();

            if (!HasApiToken())
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                // The API verifies that the enrolment
                // belongs to the logged-in Participant.
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

                    return RedirectToAction("Index");
                }

                var enrolment = await response.Content
                    .ReadFromJsonAsync<EnrolmentViewModel>();

                if (enrolment == null)
                    return NotFound();

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
                    "Unable to process enrolment information.";
            }

            return RedirectToAction("Index");
        }

        // ==========================================
        // CANCEL ENROLMENT - POST
        // Calls DELETE /api/enrolments/{id}
        // ==========================================

        [HttpPost]
        [ActionName("Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelConfirmed(int id)
        {
            if (id <= 0)
                return BadRequest();

            if (!HasApiToken())
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                // Verify the enrolment still exists
                // and belongs to this Participant.
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

                    return RedirectToAction("Index");
                }

                if (!getResponse.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to verify the selected enrolment.";

                    return RedirectToAction("Index");
                }

                var enrolment = await getResponse.Content
                    .ReadFromJsonAsync<EnrolmentViewModel>();

                if (enrolment == null)
                {
                    TempData["ErrorMessage"] =
                        "Unable to read enrolment information.";

                    return RedirectToAction("Index");
                }

                // Delete through RaceDay.API.
                // The API enforces ownership again.
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

                    return RedirectToAction("Index");
                }

                if (deleteResponse.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] =
                        "Your enrolment has been cancelled successfully!";

                    return RedirectToAction("Index");
                }

                TempData["ErrorMessage"] =
                    "Unable to cancel your enrolment. Please try again.";
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] =
                    "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                TempData["ErrorMessage"] =
                    "The request timed out. Please check whether your enrolment still exists before retrying.";
            }
            catch (JsonException)
            {
                TempData["ErrorMessage"] =
                    "Unable to read enrolment information.";
            }

            return RedirectToAction("Index");
        }
    }
}
