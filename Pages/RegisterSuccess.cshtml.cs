using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DailyLogSystem.Pages
{
    [AllowAnonymous]
    public class RegisterSuccessModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
