
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
    public class CategoryManagementController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public CategoryManagementController(
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

        // Checks whether the event belongs to
        // the currently logged-in Organiser.
        private async Task<bool> OwnsEvent(
            HttpClient client, int eventId, int organiserId)
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

        private static bool ValidAgeRange(
            CategoryViewModel model)
        {
            return !model.MinimumAge.HasValue ||
                   !model.MaximumAge.HasValue ||
                   model.MinimumAge.Value <=
                   model.MaximumAge.Value;
        }

        // ==========================================
        // CATEGORY LIST - GET
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Index(int eventId)
        {
            if (eventId <= 0)
                return BadRequest();

            var organiserId = GetOrganiserId();
            var token = HttpContext.Session.GetString("JwtToken");

            if (organiserId == null ||
                string.IsNullOrWhiteSpace(token))
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                if (!await OwnsEvent(
                    client, eventId, organiserId.Value))
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                using var response = await client.GetAsync(
                    $"api/events/{eventId}/categories");

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (!response.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] =
                        "Unable to load categories.";

                    return RedirectToAction(
                        "Details",
                        "EventManagement",
                        new { id = eventId });
                }

                var categories = await response.Content
                    .ReadFromJsonAsync<List<CategoryViewModel>>();

                ViewBag.EventId = eventId;

                return View(
                    categories ?? new List<CategoryViewModel>());
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
                    "Unable to process category information.";
            }

            return RedirectToAction(
                "Details",
                "EventManagement",
                new { id = eventId });
        }

        // ==========================================
        // CREATE CATEGORY - GET
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Create(int eventId)
        {
            if (eventId <= 0)
                return BadRequest();

            var organiserId = GetOrganiserId();

            if (organiserId == null ||
                string.IsNullOrWhiteSpace(
                    HttpContext.Session.GetString("JwtToken")))
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                if (!await OwnsEvent(
                    client, eventId, organiserId.Value))
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] =
                    "Unable to connect to RaceDay.API.";

                return RedirectToAction(
                    "Index", new { eventId });
            }
            catch (TaskCanceledException)
            {
                TempData["ErrorMessage"] =
                    "The request timed out.";

                return RedirectToAction(
                    "Index", new { eventId });
            }

            return View(new CategoryViewModel
            {
                EventId = eventId
            });
        }

        // ==========================================
        // CREATE CATEGORY - POST
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            CategoryViewModel model)
        {
            if (!ValidAgeRange(model))
            {
                ModelState.AddModelError(
                    "",
                    "Minimum age cannot exceed maximum age.");
            }

            if (!ModelState.IsValid)
                return View(model);

            var organiserId = GetOrganiserId();

            if (organiserId == null ||
                string.IsNullOrWhiteSpace(
                    HttpContext.Session.GetString("JwtToken")))
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                if (!await OwnsEvent(
                    client, model.EventId,
                    organiserId.Value))
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                var categoryData = new
                {
                    CategoryId = 0,
                    model.EventId,
                    model.CategoryName,
                    model.MinimumAge,
                    model.MaximumAge,
                    model.Distance
                };

                using var response = await client.PostAsJsonAsync(
                    $"api/events/{model.EventId}/categories",
                    categoryData);

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] =
                        "Category created successfully!";

                    return RedirectToAction(
                        "Index",
                        new { eventId = model.EventId });
                }

                if (response.StatusCode ==
                    HttpStatusCode.Forbidden)
                {
                    ModelState.AddModelError(
                        "",
                        "You do not have permission to add categories.");
                }
                else
                {
                    ModelState.AddModelError(
                        "",
                        "Unable to create category. Check the details.");
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
                    "Unable to process category information.");
            }

            return View(model);
        }

        // ==========================================
        // EDIT CATEGORY - GET
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (id <= 0)
                return BadRequest();

            var organiserId = GetOrganiserId();

            if (organiserId == null ||
                string.IsNullOrWhiteSpace(
                    HttpContext.Session.GetString("JwtToken")))
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                using var response = await client.GetAsync(
                    $"api/categories/{id}");

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (response.StatusCode ==
                    HttpStatusCode.NotFound)
                    return NotFound();

                if (!response.IsSuccessStatusCode)
                    return StatusCode(502);

                var category = await response.Content
                    .ReadFromJsonAsync<CategoryViewModel>();

                if (category == null)
                    return NotFound();

                if (!await OwnsEvent(
                    client,
                    category.EventId,
                    organiserId.Value))
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                return View(category);
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

            return RedirectToAction(
                "Index", "EventManagement");
        }

        // ==========================================
        // EDIT CATEGORY - POST
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            CategoryViewModel model)
        {
            if (id <= 0 || id != model.CategoryId)
                return BadRequest();

            if (!ValidAgeRange(model))
            {
                ModelState.AddModelError(
                    "",
                    "Minimum age cannot exceed maximum age.");
            }

            if (!ModelState.IsValid)
                return View(model);

            var organiserId = GetOrganiserId();

            if (organiserId == null ||
                string.IsNullOrWhiteSpace(
                    HttpContext.Session.GetString("JwtToken")))
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                // Get original category from API.
                using var getResponse = await client.GetAsync(
                    $"api/categories/{id}");

                if (getResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (getResponse.StatusCode ==
                    HttpStatusCode.NotFound)
                    return NotFound();

                if (!getResponse.IsSuccessStatusCode)
                {
                    ModelState.AddModelError(
                        "",
                        "Unable to verify the selected category.");

                    return View(model);
                }

                var existing = await getResponse.Content
                    .ReadFromJsonAsync<CategoryViewModel>();

                if (existing == null)
                    return NotFound();

                if (!await OwnsEvent(
                    client,
                    existing.EventId,
                    organiserId.Value))
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                var categoryData = new
                {
                    CategoryId = id,
                    EventId = existing.EventId,
                    model.CategoryName,
                    model.MinimumAge,
                    model.MaximumAge,
                    model.Distance
                };

                using var response = await client.PutAsJsonAsync(
                    $"api/categories/{id}",
                    categoryData);

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] =
                        "Category updated successfully!";

                    return RedirectToAction(
                        "Index",
                        new { eventId = existing.EventId });
                }

                ModelState.AddModelError(
                    "",
                    "Unable to update category.");
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
                    "Unable to process category information.");
            }

            return View(model);
        }

        // ==========================================
        // DELETE CATEGORY - GET
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0)
                return BadRequest();

            var organiserId = GetOrganiserId();

            if (organiserId == null ||
                string.IsNullOrWhiteSpace(
                    HttpContext.Session.GetString("JwtToken")))
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                using var response = await client.GetAsync(
                    $"api/categories/{id}");

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (response.StatusCode ==
                    HttpStatusCode.NotFound)
                    return NotFound();

                if (!response.IsSuccessStatusCode)
                    return StatusCode(502);

                var category = await response.Content
                    .ReadFromJsonAsync<CategoryViewModel>();

                if (category == null)
                    return NotFound();

                if (!await OwnsEvent(
                    client,
                    category.EventId,
                    organiserId.Value))
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                return View(category);
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

            return RedirectToAction(
                "Index", "EventManagement");
        }

        // ==========================================
        // DELETE CATEGORY - POST
        // ==========================================

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (id <= 0)
                return BadRequest();

            var organiserId = GetOrganiserId();

            if (organiserId == null ||
                string.IsNullOrWhiteSpace(
                    HttpContext.Session.GetString("JwtToken")))
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                using var getResponse = await client.GetAsync(
                    $"api/categories/{id}");

                if (getResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (getResponse.StatusCode ==
                    HttpStatusCode.NotFound)
                    return NotFound();

                if (!getResponse.IsSuccessStatusCode)
                    return StatusCode(502);

                var category = await getResponse.Content
                    .ReadFromJsonAsync<CategoryViewModel>();

                if (category == null)
                    return NotFound();

                if (!await OwnsEvent(
                    client,
                    category.EventId,
                    organiserId.Value))
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                using var response = await client.DeleteAsync(
                    $"api/categories/{id}");

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                    return await LoginAgain();

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] =
                        "Category deleted successfully!";
                }
                else if (response.StatusCode ==
                         HttpStatusCode.Conflict)
                {
                    TempData["ErrorMessage"] =
                        "This category has enrolments and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] =
                        "Unable to delete category.";
                }

                return RedirectToAction(
                    "Index",
                    new { eventId = category.EventId });
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] =
                    "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                TempData["ErrorMessage"] =
                    "The request timed out. Check whether the category still exists.";
            }
            catch (JsonException)
            {
                TempData["ErrorMessage"] =
                    "Unable to process category information.";
            }

            return RedirectToAction(
                "Index", "EventManagement");
        }
    }
}
