
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// =====================================================
// DOCKER DEVELOPMENT CONFIGURATION
// =====================================================

// This setting is enabled only for local Docker testing.
// It must not be used to disable HTTPS in production.
bool dockerEnabled = builder.Configuration.GetValue<bool>(
    "Docker:Enabled");

bool localDockerDevelopment =
    dockerEnabled && builder.Environment.IsDevelopment();

// =====================================================
// REGISTER MVC SERVICES
// =====================================================

builder.Services.AddControllersWithViews();

// =====================================================
// COOKIE AUTHENTICATION
// =====================================================

builder.Services
    .AddAuthentication("RaceDayCookie")
    .AddCookie("RaceDayCookie", options =>
    {
        options.LoginPath = "/Account/Login";

        options.AccessDeniedPath =
            "/Account/AccessDenied";

        options.ExpireTimeSpan =
            TimeSpan.FromMinutes(30);

        options.SlidingExpiration = false;

        options.Cookie.Name = "RaceDay.Auth";

        options.Cookie.HttpOnly = true;

        // HTTPS is always required normally.
        // Allow HTTP cookies only when testing Docker
        // locally in the Development environment.
        options.Cookie.SecurePolicy =
            localDockerDevelopment
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;

        options.Cookie.SameSite =
            SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();

// =====================================================
// SESSION MANAGEMENT
// =====================================================

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout =
        TimeSpan.FromMinutes(30);

    options.Cookie.HttpOnly = true;

    options.Cookie.IsEssential = true;

    options.Cookie.SecurePolicy =
        localDockerDevelopment
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

    options.Cookie.SameSite =
        SameSiteMode.Lax;
});

// =====================================================
// HTTP CLIENT - RACEDAY API
// =====================================================

// Normal Visual Studio configuration:
// ApiSettings:BaseUrl comes from appsettings.json.
//
// Docker configuration:
// ApiSettings__BaseUrl can be supplied using
// a Docker environment variable.

builder.Services.AddHttpClient(
    "RaceDayAPI",
    client =>
    {
        var apiBaseUrl =
            builder.Configuration["ApiSettings:BaseUrl"];

        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            throw new InvalidOperationException(
                "API Base URL is not configured.");
        }

        if (!Uri.TryCreate(
                apiBaseUrl,
                UriKind.Absolute,
                out var apiUri) ||
            (apiUri.Scheme != Uri.UriSchemeHttp &&
             apiUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "API Base URL must be a valid HTTP or HTTPS URL.");
        }

        client.BaseAddress = apiUri;
    });

// =====================================================
// APPLICATION SERVICES
// =====================================================

builder.Services
    .AddScoped<RaceDay.MVC.Services.ApiService>();

// =====================================================
// BUILD APPLICATION
// =====================================================

var app = builder.Build();

// =====================================================
// MIDDLEWARE PIPELINE
// =====================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();
}

// Normal applications use HTTPS redirection.
// The local development Docker test runs over HTTP.
// Production must use HTTPS or a properly configured
// TLS-terminating reverse proxy.

if (!localDockerDevelopment)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();

app.UseAuthorization();

// =====================================================
// MVC ROUTES
// =====================================================

app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Account}/{action=Login}/{id?}");

app.Run();
