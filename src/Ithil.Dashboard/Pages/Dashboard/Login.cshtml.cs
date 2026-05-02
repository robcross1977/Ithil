using Ithil.Dashboard.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace Ithil.Dashboard.Pages.Dashboard;

/// <summary>
/// Handles login for the operator dashboard.
/// GET serves the token entry form; POST validates the JWT and issues the session cookie.
/// </summary>
public class LoginModel(TokenValidationParameters tokenValidationParameters) : PageModel
{
    [BindProperty]
    public string Token { get; set; } = string.Empty;

    public string? ErrorMessage { get; private set; }

    public static void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(Token, tokenValidationParameters);

        if (!result.IsValid)
        {
            ErrorMessage = "Invalid token.";
            return Page();
        }

        if (!result.ClaimsIdentity.HasClaim("scope", "admin"))
        {
            ErrorMessage = "Token does not have admin scope.";
            return Page();
        }

        var identity = new ClaimsIdentity(
            result.ClaimsIdentity.Claims,
            DashboardAuthPolicy.CookieScheme);

        await HttpContext.SignInAsync(
            DashboardAuthPolicy.CookieScheme,
            new ClaimsPrincipal(identity));

        return Redirect("/dashboard");
    }
}
