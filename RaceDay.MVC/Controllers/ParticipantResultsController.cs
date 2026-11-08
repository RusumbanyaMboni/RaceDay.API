
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
    public class ParticipantResultsController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public ParticipantResultsController(
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
        // MY RACE RESULTS
        // GET /ParticipantResults/Index
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
                    "api/results/my-results");

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
                        "Unable to load your race results.";

                    return View(new List<ResultViewModel>());
                }

                var results = await response.Content
                    .ReadFromJsonAsync<List<ResultViewModel>>();

                return View(
                    (results ?? new List<ResultViewModel>())
                    .OrderByDescending(r => r.RecordedAt)
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
                    "Unable to process your race results.";
            }

            return View(new List<ResultViewModel>());
        }
    }
}
