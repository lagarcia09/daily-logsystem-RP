using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DailyLogSystem.Pages
{
    public class LogoutModel : PageModel
    {
        public IActionResult OnGet()
        {
            // Clear session completely
            HttpContext.Session.Clear();

            // Redirect to login/index page
            return RedirectToPage("/Index");
        }
    }
}
