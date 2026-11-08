
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.MVC.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace RaceDay.MVC.Controllers
{
    public class AccountController : Controller
    {
        private const string CookieScheme = "RaceDayCookie";

        private readonly IHttpClientFactory _httpClientFactory;

        public AccountController(
            IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // ==========================================
        // LOGIN PAGE
        // ==========================================

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Organiser"))
                    return RedirectToAction("Index", "Organiser");

                if (User.IsInRole("Participant"))
                    return RedirectToAction("Index", "Participant");
            }

            return View(new LoginViewModel());
        }

        // ==========================================
        // PROCESS LOGIN
        // ==========================================

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var client = _httpClientFactory
                    .CreateClient("RaceDayAPI");

                using var response =
                    await client.PostAsJsonAsync(
                        "api/Auth/login",
                        new
                        {
                            model.Email,
                            model.Password
                        });

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    ModelState.AddModelError(
                        "",
                        "Invalid email or password.");

                    return View(model);
                }

                if (!response.IsSuccessStatusCode)
                {
                    ModelState.AddModelError(
                        "",
                        "Login could not be completed. Please try again.");

                    return View(model);
                }

                var loginResult = await response.Content
                    .ReadFromJsonAsync<LoginResponse>();

                if (loginResult == null ||
                    string.IsNullOrWhiteSpace(loginResult.Token) ||
                    loginResult.User == null)
                {
                    ModelState.AddModelError(
                        "",
                        "Unable to process the login response.");

                    return View(model);
                }

                bool isOrganiser =
                    loginResult.User.RoleId == 1 &&
                    string.Equals(
                        loginResult.User.Role,
                        "Organiser",
                        StringComparison.OrdinalIgnoreCase);

                bool isParticipant =
                    loginResult.User.RoleId == 2 &&
                    string.Equals(
                        loginResult.User.Role,
                        "Participant",
                        StringComparison.OrdinalIgnoreCase);

                if (!isOrganiser && !isParticipant)
                {
                    ModelState.AddModelError(
                        "",
                        "Your account has an unsupported role.");

                    return View(model);
                }

                // Read the JWT expiration time.
                // The token is issued by RaceDay.API.
                var tokenHandler = new JwtSecurityTokenHandler();

                var jwt = tokenHandler.ReadJwtToken(
                    loginResult.Token);

                if (jwt.ValidTo <= DateTime.UtcNow)
                {
                    ModelState.AddModelError(
                        "",
                        "The authentication token has expired.");

                    return View(model);
                }

                // Verify the returned JWT with the API
                // before establishing an MVC identity.
                using var verifyRequest =
                    new HttpRequestMessage(
                        HttpMethod.Get,
                        "api/Auth/me");

                verifyRequest.Headers.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer",
                        loginResult.Token);

                using var verifyResponse =
                    await client.SendAsync(verifyRequest);

                if (!verifyResponse.IsSuccessStatusCode)
                {
                    ModelState.AddModelError(
                        "",
                        "Unable to verify your login. Please try again.");

                    return View(model);
                }

                var verifiedUser =
                    await verifyResponse.Content
                        .ReadFromJsonAsync<LoginUser>();

                if (verifiedUser == null ||
                    verifiedUser.UserId != loginResult.User.UserId ||
                    verifiedUser.RoleId != loginResult.User.RoleId ||
                    !string.Equals(
                        verifiedUser.Email,
                        loginResult.User.Email,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        verifiedUser.Role,
                        loginResult.User.Role,
                        StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(
                        "",
                        "The authenticated user details could not be verified.");

                    return View(model);
                }

                string role = isOrganiser
                    ? "Organiser"
                    : "Participant";

                // Create authenticated MVC identity
                var claims = new List<Claim>
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        verifiedUser.UserId.ToString()),

                    new Claim(
                        ClaimTypes.Name,
                        verifiedUser.FirstName),

                    new Claim(
                        ClaimTypes.Email,
                        verifiedUser.Email),

                    new Claim(
                        ClaimTypes.Role,
                        role)
                };

                var identity = new ClaimsIdentity(
                    claims,
                    CookieScheme);

                var principal = new ClaimsPrincipal(identity);

                // The MVC cookie must not outlive
                // the JWT or the 30-minute login window.
                var cookieExpiry = DateTime.UtcNow
                    .AddMinutes(30);

                if (jwt.ValidTo < cookieExpiry)
                    cookieExpiry = jwt.ValidTo;

                // Clear previous session data
                HttpContext.Session.Clear();

                // Save JWT for subsequent API requests
                HttpContext.Session.SetString(
                    "JwtToken",
                    loginResult.Token);

                HttpContext.Session.SetInt32(
                    "UserId",
                    verifiedUser.UserId);

                HttpContext.Session.SetString(
                    "FirstName",
                    verifiedUser.FirstName);

                HttpContext.Session.SetString(
                    "LastName",
                    verifiedUser.LastName);

                HttpContext.Session.SetString(
                    "Email",
                    verifiedUser.Email);

                HttpContext.Session.SetInt32(
                    "RoleId",
                    verifiedUser.RoleId);

                HttpContext.Session.SetString(
                    "Role",
                    role);

                // Create MVC authentication cookie
                await HttpContext.SignInAsync(
                    CookieScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = false,
                        ExpiresUtc =
                            new DateTimeOffset(cookieExpiry)
                    });

                // Redirect to role-specific dashboard
                if (isOrganiser)
                {
                    return RedirectToAction(
                        "Index",
                        "Organiser");
                }

                return RedirectToAction(
                    "Index",
                    "Participant");
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
                    "The login request timed out.");
            }
            catch (JsonException)
            {
                ModelState.AddModelError(
                    "",
                    "The API returned an invalid response.");
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(
                    "",
                    "The API returned an invalid authentication token.");
            }

            return View(model);
        }

        // ==========================================
        // REGISTRATION PAGE
        // ==========================================

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        // ==========================================
        // PROCESS REGISTRATION
        // ==========================================

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var client = _httpClientFactory
                    .CreateClient("RaceDayAPI");

                var registerData = new
                {
                    model.FirstName,
                    model.LastName,
                    model.Email,
                    model.Password,
                    model.PhoneNumber,
                    model.DateOfBirth,
                    model.RoleId
                };

                using var response =
                    await client.PostAsJsonAsync(
                        "api/Auth/register",
                        registerData);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] =
                        "Registration successful! Please log in.";

                    return RedirectToAction(
                        "Login",
                        "Account");
                }

                if (response.StatusCode ==
                    HttpStatusCode.Conflict)
                {
                    ModelState.AddModelError(
                        nameof(model.Email),
                        "This email address is already registered.");
                }
                else if (response.StatusCode ==
                         HttpStatusCode.BadRequest)
                {
                    ModelState.AddModelError(
                        "",
                        "Registration details were rejected. " +
                        "Please check your information.");
                }
                else
                {
                    ModelState.AddModelError(
                        "",
                        "Registration could not be completed. " +
                        "Please try again.");
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
                    "The registration request timed out.");
            }

            return View(model);
        }

        // ==========================================
        // LOGOUT
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieScheme);

            HttpContext.Session.Clear();

            return RedirectToAction(
                "Login",
                "Account");
        }

        // ==========================================
        // ACCESS DENIED
        // ==========================================

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
