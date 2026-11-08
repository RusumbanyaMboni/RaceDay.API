
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

        // ==========================================
        // REGISTER USER
        // ==========================================

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var email = request.Email.Trim().ToLowerInvariant();

            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (existingUser != null)
            {
                return Conflict(new
                {
                    message = "Email address is already registered."
                });
            }

            var role = await _context.Roles
                .FirstOrDefaultAsync(r =>
                    r.RoleId == request.RoleId);

            if (role == null ||
                (role.RoleName != "Organiser" &&
                 role.RoleName != "Participant"))
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
                Email = email,
                PhoneNumber = request.PhoneNumber,
                DateOfBirth = request.DateOfBirth,
                RoleId = role.RoleId,

                PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                    request.Password),

                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return StatusCode(201, new
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

        // ==========================================
        // LOGIN USER
        // ==========================================

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var email = request.Email.Trim().ToLowerInvariant();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

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

            var token = GenerateJwtToken(
                user,
                role.RoleName);

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

        // ==========================================
        // GET AUTHENTICATED USER
        // ==========================================

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
                    message =
                        "Unable to identify the authenticated user."
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

            var role = await _context.Roles
                .FirstOrDefaultAsync(r =>
                    r.RoleId == user.RoleId);

            if (role == null)
            {
                return NotFound(new
                {
                    message = "User role not found."
                });
            }

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

        // ==========================================
        // GENERATE JWT TOKEN
        // ==========================================

        private string GenerateJwtToken(
            User user,
            string roleName)
        {
            var jwtKey = _configuration["Jwt:Key"];

            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];

            if (string.IsNullOrWhiteSpace(jwtKey) ||
                string.IsNullOrWhiteSpace(issuer) ||
                string.IsNullOrWhiteSpace(audience))
            {
                throw new InvalidOperationException(
                    "JWT configuration is incomplete.");
            }

            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.UserId.ToString()),

                new Claim(
                    ClaimTypes.Email,
                    user.Email),

                new Claim(
                    ClaimTypes.Name,
                    user.FirstName),

                // Numeric role identifier
                new Claim(
                    "RoleId",
                    user.RoleId.ToString()),

                // Role name for [Authorize(Roles = "...")]
                new Claim(
                    ClaimTypes.Role,
                    roleName),

                new Claim(
                    "RoleName",
                    roleName)
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey));

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }

        // ==========================================
        // REGISTER REQUEST MODEL
        // ==========================================

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

            [Range(1, 2,
                ErrorMessage = "Select a valid account type.")]
            public int RoleId { get; set; }
        }

        // ==========================================
        // LOGIN REQUEST MODEL
        // ==========================================

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
