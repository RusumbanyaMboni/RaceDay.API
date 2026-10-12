using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RaceDay.API.Data;
using RaceDay.API.Models;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RaceDay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(
            RaceDayDbContext context,
            IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }


            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Email.ToLower() == request.Email.ToLower());

            if (existingUser != null)
            {
                return Conflict(new
                {
                    message = "Email address is already registered."
                });
            }



            var role = await _context.Roles
                .FirstOrDefaultAsync(r => r.RoleId == request.RoleId);

            if (role == null)
            {
                return BadRequest(new
                {
                    message = "Invalid role selected."
                });
            }


            var user = new User
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = request.Email.Trim().ToLower(),
                PhoneNumber = request.PhoneNumber,
                DateOfBirth = request.DateOfBirth,
                RoleId = request.RoleId,

            
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                    request.Password),

                
                CreatedAt = DateTime.UtcNow
            };


            _context.Users.Add(user);

            await _context.SaveChangesAsync();



            return CreatedAtAction(
                nameof(Register),
                new { id = user.UserId },
                new
                {
                    message = "Registration successful.",
                    userId = user.UserId,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    phoneNumber = user.PhoneNumber,
                    dateOfBirth = user.DateOfBirth,
                    roleId = user.RoleId,
                    role = role.RoleName
                });
        }


        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }



            var user = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Email.ToLower() == request.Email.ToLower());

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }


            bool passwordValid;

            try
            {
                passwordValid = BCrypt.Net.BCrypt.Verify(
                    request.Password,
                    user.PasswordHash);
            }
            catch
            {
                passwordValid = false;
            }

            if (!passwordValid)
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }



            var role = await _context.Roles
                .FirstOrDefaultAsync(r =>
                    r.RoleId == user.RoleId);

            if (role == null)
            {
                return Unauthorized(new
                {
                    message = "User role could not be determined."
                });
            }


            var token = GenerateJwtToken(user, role.RoleName);



            return Ok(new
            {
                message = "Login successful.",

                token = token,

                user = new
                {
                    userId = user.UserId,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    phoneNumber = user.PhoneNumber,
                    dateOfBirth = user.DateOfBirth,
                    roleId = user.RoleId,
                    role = role.RoleName
                }
            });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> AuthMe()
        {
    
            var userIdClaim =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst("UserId")?.Value;

            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new
                {
                    message = "Unable to identify the authenticated user."
                });
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User not found."
                });
            }

            // Get the user's role
            var role = await _context.Roles
                .FirstOrDefaultAsync(r => r.RoleId == user.RoleId);

            if (role == null)
            {
                return NotFound(new
                {
                    message = "User role not found."
                });
            }

            // Return current user information
            return Ok(new
            {
                userId = user.UserId,
                firstName = user.FirstName,
                lastName = user.LastName,
                email = user.Email,
                phoneNumber = user.PhoneNumber,
                dateOfBirth = user.DateOfBirth,
                roleId = user.RoleId,
                role = role.RoleName
            });
        }

        // =========================================================
        // GENERATE JWT TOKEN
        // =========================================================

        private string GenerateJwtToken(User user, string roleName)
        {
            // -----------------------------------------------------
            // Get JWT configuration
            // -----------------------------------------------------

            var jwtKey =
                _configuration["Jwt:Key"]
                ?? _configuration["Jwt:Secret"]
                ?? _configuration["JwtKey"];

            if (string.IsNullOrWhiteSpace(jwtKey))
            {
                throw new InvalidOperationException(
                    "JWT key is not configured. " +
                    "Add Jwt:Key to appsettings.json.");
            }


            var issuer =
                _configuration["Jwt:Issuer"]
                ?? "RaceDay.API";

            var audience =
                _configuration["Jwt:Audience"]
                ?? "RaceDay.Client";


           

            var claims = new List<Claim>
{
    // User ID
    new Claim(
        ClaimTypes.NameIdentifier,
        user.UserId.ToString()),

    // Email
    new Claim(
        ClaimTypes.Email,
        user.Email),

    // First name
    new Claim(
        ClaimTypes.Name,
        user.FirstName),

    // Role ID
    new Claim(
        "RoleId",
        user.RoleId.ToString()),

    // ASP.NET authorization role
    new Claim(
        ClaimTypes.Role,
        user.RoleId.ToString()),

    // Role name
    new Claim(
        "RoleName",
        roleName)
};


            // -----------------------------------------------------
            // Create signing key
            // -----------------------------------------------------

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey));

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);


            // -----------------------------------------------------
            // Create token
            // -----------------------------------------------------

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: credentials);


            // -----------------------------------------------------
            // Return token string
            // -----------------------------------------------------

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }


        // =========================================================
        // REGISTER REQUEST
        //
        // This is intentionally separate from User.
        //
        // The API receives "password".
        // The database stores "PasswordHash".
        // =========================================================

        public class RegisterRequest
        {
            [Required]
            public string FirstName { get; set; } = string.Empty;


            [Required]
            public string LastName { get; set; } = string.Empty;


            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;


            [Required]
            [MinLength(6)]
            public string Password { get; set; } = string.Empty;


            public string? PhoneNumber { get; set; }


            public DateTime? DateOfBirth { get; set; }


            [Required]
            public int RoleId { get; set; }
        }


        // =========================================================
        // LOGIN REQUEST
        // =========================================================

        public class LoginRequest
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;


            [Required]
            public string Password { get; set; } = string.Empty;
        }
    }
}
