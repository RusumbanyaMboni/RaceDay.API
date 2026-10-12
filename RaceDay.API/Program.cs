using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RaceDay.API.Data;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("RaceDayConnection")
    ));



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
           

            ValidateIssuerSigningKey = true,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            ),


            

            ValidateIssuer = true,

            ValidIssuer = jwtIssuer,


           
            ValidateAudience = true,

            ValidAudience = jwtAudience,


           

            ValidateLifetime = true,

            ClockSkew = TimeSpan.Zero,


            
            RoleClaimType = "RoleId",


            NameClaimType = ClaimTypes.Name
        };


        options.Events = new JwtBearerEvents
        {
           

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


          

            OnTokenValidated = context =>
            {
                Console.WriteLine(
                    "========================================"
                );

                Console.WriteLine(
                    "JWT TOKEN VALIDATED"
                );


                
                var roleId = context.Principal?
                    .FindFirst("RoleId")?.Value;

                Console.WriteLine(
                    "JWT ROLE ID: " + roleId
                );


              
                var userId = context.Principal?
                    .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                Console.WriteLine(
                    "JWT USER ID: " + userId
                );


               
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




builder.Services.AddAuthorization();




builder.Services.AddControllers();



builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
  

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



var app = builder.Build();




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
