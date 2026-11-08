
using Microsoft.AspNetCore.Mvc;
using RaceDay.MVC.Services;

namespace RaceDay.MVC.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApiService _apiService;

        public HomeController(ApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<IActionResult> Index()
        {
            bool isConnected =
                await _apiService.IsApiAvailableAsync();

            ViewBag.ApiStatus = isConnected
                ? "Connected to RaceDay.API successfully!"
                : "Unable to connect to RaceDay.API.";

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
