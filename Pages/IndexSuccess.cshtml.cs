using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DailyLogSystem.Pages
{
    [AllowAnonymous]
    public class IndexSuccessModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
