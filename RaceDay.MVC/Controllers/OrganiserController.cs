
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
    public class OrganiserController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public OrganiserController(
            IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // ==========================================
        // CREATE AUTHENTICATED API CLIENT
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
        // EXPIRED SESSION
        // ==========================================

        private async Task<IActionResult> LoginAgain()
        {
            await HttpContext.SignOutAsync("RaceDayCookie");

            HttpContext.Session.Clear();

            return RedirectToAction("Login", "Account");
        }

        // ==========================================
        // ORGANISER DASHBOARD
        // GET /Organiser/Index
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = new OrganiserDashboardViewModel
            {
                FirstName = User.Identity?.Name ?? "Organiser"
            };

            var token = HttpContext.Session.GetString("JwtToken");

            if (string.IsNullOrWhiteSpace(token))
                return await LoginAgain();

            var userIdValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdValue, out int organiserId))
                return await LoginAgain();

            try
            {
                var client = CreateApiClient();

                // ==================================
                // LOAD ALL EVENTS
                // ==================================

                using var eventsResponse = await client.GetAsync(
                    "api/Event");

                if (eventsResponse.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    return await LoginAgain();
                }

                if (eventsResponse.StatusCode ==
                    HttpStatusCode.Forbidden)
                {
                    return RedirectToAction(
                        "AccessDenied", "Account");
                }

                if (!eventsResponse.IsSuccessStatusCode)
                {
                    model.StatisticsAvailable = false;

                    ViewBag.ErrorMessage =
                        "Unable to load dashboard statistics.";

                    return View(model);
                }

                var allEvents = await eventsResponse.Content
                    .ReadFromJsonAsync<List<EventViewModel>>();

                if (allEvents == null)
                {
                    model.StatisticsAvailable = false;

                    ViewBag.ErrorMessage =
                        "Unable to read event information.";

                    return View(model);
                }

                // Only include events owned by the
                // authenticated Organiser.

                var myEvents = allEvents
                    .Where(e => e.OrganiserId == organiserId)
                    .ToList();

                model.TotalEvents = myEvents.Count;

                // ==================================
                // LOAD ENROLMENTS AND RESULTS
                // FOR EACH OWNED EVENT
                // ==================================

                foreach (var eventItem in myEvents)
                {
                    // Get event enrolments.

                    using var enrolmentsResponse =
                        await client.GetAsync(
                            $"api/events/{eventItem.EventId}/enrolments");

                    if (enrolmentsResponse.StatusCode ==
                        HttpStatusCode.Unauthorized)
                    {
                        return await LoginAgain();
                    }

                    if (enrolmentsResponse.StatusCode ==
                        HttpStatusCode.Forbidden)
                    {
                        return RedirectToAction(
                            "AccessDenied", "Account");
                    }

                    if (!enrolmentsResponse.IsSuccessStatusCode)
                    {
                        model.StatisticsAvailable = false;
                        break;
                    }

                    var enrolments = await enrolmentsResponse.Content
                        .ReadFromJsonAsync<
                            List<OrganiserEnrolmentViewModel>>();

                    if (enrolments == null)
                    {
                        model.StatisticsAvailable = false;
                        break;
                    }

                    model.TotalEnrolments += enrolments.Count;

                    model.ConfirmedEnrolments += enrolments.Count(
                        e => string.Equals(
                            e.Status,
                            "Confirmed",
                            StringComparison.OrdinalIgnoreCase));

                    // Get recorded event results.

                    using var resultsResponse =
                        await client.GetAsync(
                            $"api/events/{eventItem.EventId}/results");

                    if (resultsResponse.StatusCode ==
                        HttpStatusCode.Unauthorized)
                    {
                        return await LoginAgain();
                    }

                    if (!resultsResponse.IsSuccessStatusCode)
                    {
                        model.StatisticsAvailable = false;
                        break;
                    }

                    var results = await resultsResponse.Content
                        .ReadFromJsonAsync<List<ResultViewModel>>();

                    if (results == null)
                    {
                        model.StatisticsAvailable = false;
                        break;
                    }

                    model.TotalResults += results.Count;
                }

                if (!model.StatisticsAvailable)
                {
                    ViewBag.ErrorMessage =
                        "Some dashboard statistics could not be loaded. " +
                        "Please refresh the page and try again.";
                }

                return View(model);
            }
            catch (HttpRequestException)
            {
                ViewBag.ErrorMessage =
                    "Unable to connect to RaceDay.API.";
            }
            catch (TaskCanceledException)
            {
                ViewBag.ErrorMessage =
                    "The request timed out while loading statistics.";
            }
            catch (JsonException)
            {
                ViewBag.ErrorMessage =
                    "Unable to process dashboard data.";
            }

            model.StatisticsAvailable = false;

            return View(model);
        }
    }
}
