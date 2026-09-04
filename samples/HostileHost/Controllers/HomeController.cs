using Microsoft.AspNetCore.Mvc;

namespace HostileHost.Controllers;

/// <summary>
/// One page, on purpose. The kildeutforsker and the search-only mount have hosts of their own;
/// what this sample adds is helsedata's chrome around the composed <c>VariableExplorer</c>.
/// </summary>
public class HomeController : Controller
{
    public IActionResult Index() => View();
}
