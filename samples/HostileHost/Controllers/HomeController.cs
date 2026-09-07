using Microsoft.AspNetCore.Mvc;

namespace HostileHost.Controllers;

/// <summary>
/// Two pages: the composed <c>VariableExplorer</c> and the kildeutforsker, both under helsedata's
/// own chrome and stylesheet. The search-only mount has hosts of its own and is not here.
/// </summary>
public class HomeController : Controller
{
    public IActionResult Index() => View();

    /// <summary>The kilder table, which is the widest thing the package draws.</summary>
    public IActionResult Kilder() => View();
}
