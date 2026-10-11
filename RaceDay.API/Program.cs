using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RaceDay.API.Data;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// =========================================================
// DATABASE
// =========================================================

builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("RaceDayConnection")
    ));


// =========================================================
// JWT AUTHENTICATION
// =========================================================

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "JWT key is missing. Add Jwt:Key to appsettings.json."
    );
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // =====================================================
            // SIGNING KEY
            // =====================================================

            ValidateIssuerSigningKey = true,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            ),


            // =====================================================
            // ISSUER
            // =====================================================

            ValidateIssuer = true,

            ValidIssuer = jwtIssuer,


            // =====================================================
            // AUDIENCE
            // =====================================================

            ValidateAudience = true,

            ValidAudience = jwtAudience,


            // =====================================================
            // TOKEN LIFETIME
            // =====================================================

            ValidateLifetime = true,

            ClockSkew = TimeSpan.Zero,


            // =====================================================
            // IMPORTANT ROLE CONFIGURATION
            //
            // AuthController creates:
            //
            // new Claim("RoleId", user.RoleId.ToString())
            //
            // Therefore we explicitly tell ASP.NET Core
            // to use "RoleId" for:
            //
            // [Authorize(Roles = "1")]
            // [Authorize(Roles = "2")]
            // =====================================================

            RoleClaimType = "RoleId",


            // =====================================================
            // USER NAME CLAIM
            // =====================================================

            NameClaimType = ClaimTypes.Name
        };


        // =====================================================
        // JWT EVENTS
        // =====================================================

        options.Events = new JwtBearerEvents
        {
            // -----------------------------------------------------
            // Authentication failure
            // -----------------------------------------------------

            OnAuthenticationFailed = context =>
            {
                Console.WriteLine(
                    "========================================"
                );

                Console.WriteLine(
                    "JWT AUTHENTICATION FAILED"
                );

                Console.WriteLine(
                    "ERROR: " + context.Exception.Message
                );

                Console.WriteLine(
                    "========================================"
                );

                return Task.CompletedTask;
            },


            // -----------------------------------------------------
            // Token successfully validated
            // -----------------------------------------------------

            OnTokenValidated = context =>
            {
                Console.WriteLine(
                    "========================================"
                );

                Console.WriteLine(
                    "JWT TOKEN VALIDATED"
                );


                // Get RoleId from JWT
                var roleId = context.Principal?
                    .FindFirst("RoleId")?.Value;

                Console.WriteLine(
                    "JWT ROLE ID: " + roleId
                );


                // Get user ID from JWT
                var userId = context.Principal?
                    .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                Console.WriteLine(
                    "JWT USER ID: " + userId
                );


                // Get email from JWT
                var email = context.Principal?
                    .FindFirst(ClaimTypes.Email)?.Value;

                Console.WriteLine(
                    "JWT EMAIL: " + email
                );


                Console.WriteLine(
                    "========================================"
                );

                return Task.CompletedTask;
            }
        };
    });


// =========================================================
// AUTHORIZATION
// =========================================================

builder.Services.AddAuthorization();


// =========================================================
// CONTROLLERS
// =========================================================

builder.Services.AddControllers();


// =========================================================
// SWAGGER
// =========================================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    // ---------------------------------------------------------
    // Bearer Authentication
    // ---------------------------------------------------------

    options.AddSecurityDefinition(
        "Bearer",
        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",

            Type =
                Microsoft.OpenApi.Models.SecuritySchemeType.Http,

            Scheme = "bearer",

            BearerFormat = "JWT",

            In =
                Microsoft.OpenApi.Models.ParameterLocation.Header,

            Description =
                "Enter your JWT token."
        });


    // ---------------------------------------------------------
    // Apply Bearer authentication to Swagger endpoints
    // ---------------------------------------------------------

    options.AddSecurityRequirement(
        new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference =
                        new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type =
                                Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,

                            Id = "Bearer"
                        }
                },

                Array.Empty<string>()
            }
        });
});


// =========================================================
// BUILD APPLICATION
// =========================================================

var app = builder.Build();


// =========================================================
// SWAGGER
// =========================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}


// =========================================================
// HTTPS
// =========================================================

app.UseHttpsRedirection();


// =========================================================
// AUTHENTICATION
//
// IMPORTANT:
// Authentication MUST come before Authorization.
// =========================================================

app.UseAuthentication();


// =========================================================
// AUTHORIZATION
// =========================================================

app.UseAuthorization();


// =========================================================
// CONTROLLERS
// =========================================================

app.MapControllers();


// =========================================================
// RUN APPLICATION
// =========================================================

app.Run();