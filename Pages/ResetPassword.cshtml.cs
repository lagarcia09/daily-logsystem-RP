using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DailyLogSystem.Services;

namespace DailyLogSystem.Pages
{
    public class ResetPasswordModel : PageModel
    {
        private readonly MongoDbService _mongoService;
        private readonly ITokenStore _tokenStore;

        public ResetPasswordModel(MongoDbService mongoService, ITokenStore tokenStore)
        {
            _mongoService = mongoService;
            _tokenStore = tokenStore;
        }

        [BindProperty] public string NewPassword { get; set; } = "";
        [BindProperty] public string ConfirmPassword { get; set; } = "";

        public string? ErrorMessage { get; set; }
        public string? Message { get; set; }

        public async Task<IActionResult> OnPostAsync(string token)
        {
            var record = _tokenStore.Get(token);

            if (record == null || record.Expiry < DateTime.UtcNow)
            {
                ErrorMessage = "Invalid or expired reset link.";
                return Page();
            }

            if (NewPassword != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                return Page();
            }

            var user = await _mongoService.GetUserByEmailAsync(record.Email);
            if (user == null)
            {
                ErrorMessage = "User not found.";
                return Page();
            }

            user.Password = NewPassword;
            await _mongoService.UpdateUserAsync(user);

            Message = "Password successfully reset! You can now log in.";

            _tokenStore.MarkUsed(token);

            return Page();
        }
    }
}
