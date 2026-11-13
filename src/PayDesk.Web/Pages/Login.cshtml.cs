using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PayDesk.Web.Models;
using PayDesk.Web.Services;

namespace PayDesk.Web.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly PayDeskApiClient _api;

    public LoginModel(PayDeskApiClient api)
    {
        _api = api;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var result = await _api.LoginAsync(Input.Email, Input.Password);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, result.EmployeeId.ToString()),
                new(ClaimTypes.Name, result.FullName),
                new(ClaimTypes.Role, result.Role),
                new("jwt", result.Token)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            return RedirectToPage("/Index");
        }
        catch (ApiProblemException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    public class LoginInput
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
