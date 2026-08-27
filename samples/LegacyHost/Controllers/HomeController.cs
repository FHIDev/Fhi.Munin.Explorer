using System.Diagnostics;
using LegacyHost.Models;
using Microsoft.AspNetCore.Mvc;

namespace LegacyHost.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// The kildeutforsker, on a route of its own. The package's two root components are separate
    /// entry points rather than two views of one page, so each gets its own action here.
    /// </summary>
    public IActionResult Kilder()
    {
        return View();
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
