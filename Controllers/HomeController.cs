using System.Diagnostics;
using Freelancing.Data;
using Freelancing.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Freelancing.Controllers
{
    // Handles the home page and redirects users based on their roles.
    public class HomeController : Controller
    {

        private readonly ApplicationDbContext _context;
        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }
        // Displays the home page and redirects users based on their roles.
        public IActionResult Index()
        {
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value?.ToLower();
            if (role == "client")
            {
                return RedirectToAction("Dashboard", "Client");
            }
            else if (role == "freelancer")
            {
                return RedirectToAction("Dashboard", "Freelancer");
            }
            else if (role == null)
            {
                return View();
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
