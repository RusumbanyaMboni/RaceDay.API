
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RaceDay.MVC.Controllers
{
    [Authorize(Roles = "Participant")]
    public class ParticipantController : Controller
    {
        public IActionResult Index()
        {
            ViewBag.FirstName = User.Identity?.Name;

            return View();
        }
    }
}
