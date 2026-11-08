
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RaceDay.API.Data;
using RaceDay.API.Services;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// DATABASE CONFIGURATION
// ==========================================

builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString(
            "RaceDayConnection")
    ));

// ==========================================
// AZURE BLOB STORAGE CONFIGURATION
// ==========================================

// The connection string and container names are
// loaded from configuration, including User Secrets.
//
// Required User Secrets:
// AzureStorage:ConnectionString
// AzureStorage:ProfilePicturesContainer
// AzureStorage:EventImagesContainer

builder.Services.AddSingleton<AzureBlobStorageService>();

// ==========================================
// JWT CONFIGURATION
// ==========================================

var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(jwtKey) ||
    string.IsNullOrWhiteSpace(jwtIssuer) ||
    string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException(
        "JWT configuration is missing. " +
        "Check Jwt:Key, Jwt:Issuer and Jwt:Audience.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)),

                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,

                // Use the role name instead of numeric RoleId.
                RoleClaimType = ClaimTypes.Role,

                NameClaimType = ClaimTypes.Name
            };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                if (builder.Environment.IsDevelopment())
                {
                    Console.WriteLine(
                        "JWT AUTHENTICATION FAILED: " +
                        context.Exception.Message);
                }

                return Task.CompletedTask;
            },

            OnTokenValidated = context =>
            {
                if (builder.Environment.IsDevelopment())
                {
                    var role = context.Principal?
                        .FindFirst(ClaimTypes.Role)?.Value;

                    var roleId = context.Principal?
                        .FindFirst("RoleId")?.Value;

                    Console.WriteLine(
                        "JWT TOKEN VALIDATED");

                    Console.WriteLine(
                        "JWT ROLE: " + role);

                    Console.WriteLine(
                        "JWT ROLE ID: " + roleId);
                }

                return Task.CompletedTask;
            }
        };
    });

// ==========================================
// AUTHORISATION
// ==========================================

builder.Services.AddAuthorization();

// ==========================================
// CONTROLLERS
// ==========================================

builder.Services.AddControllers();

// ==========================================
// SWAGGER CONFIGURATION
// ==========================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token."
        });

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
});

// ==========================================
// BUILD APPLICATION
// ==========================================

var app = builder.Build();

// ==========================================
// SWAGGER
// ==========================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ==========================================
// HTTP PIPELINE
// ==========================================

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
