using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace RaceDay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrganizerController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            // Get the user's role from the JWT
            var role =
                User.FindFirst(ClaimTypes.Role)?.Value
                ?? User.FindFirst("role")?.Value;

            // Organizer = RoleId 1
            if (role != "1")
            {
                return Forbid();
            }

            return Ok(new
            {
                message = "Welcome Organizer. You have access.",
                roleId = 1,
                role = "Organizer"
            });
        }
    }
}