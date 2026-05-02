using Ithil.Dashboard.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ithil.Dashboard.Pages.Dashboard;

/// <summary>
/// Signs the operator out by clearing the dashboard session cookie
/// and redirecting to the login page.
/// </summary>
public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        await HttpContext.SignOutAsync(DashboardAuthPolicy.CookieScheme);
        return Redirect("/dashboard/login");
    }
}
