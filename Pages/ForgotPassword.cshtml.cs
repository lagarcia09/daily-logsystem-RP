using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DailyLogSystem.Services;
using DailyLogSystem.Models;

namespace DailyLogSystem.Pages
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly MongoDbService _mongoService;
        private readonly EmailService _emailService;
        private readonly ITokenStore _tokenStore;

        public ForgotPasswordModel(MongoDbService mongoService, EmailService emailService, ITokenStore tokenStore)
        {
            _mongoService = mongoService;
            _emailService = emailService;
            _tokenStore = tokenStore;
        }

        [BindProperty]
        public string Email { get; set; } = "";


        public string? Message { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                ErrorMessage = "Please enter your email.";
                return Page();
            }

            var user = await _mongoService.GetUserByEmailAsync(Email);
            if (user == null)
            {
                ErrorMessage = "Email not found.";
                return Page();
            }

            // Create reset token
            var token = Guid.NewGuid().ToString("N");

            _tokenStore.Add(new LoginToken
            {
                Token = token,
                Email = Email,
                Role = "ResetPassword",
                Expiry = DateTime.UtcNow.AddMinutes(10)
            });

            var resetUrl = Url.Page("/ResetPassword", null, new { token }, Request.Scheme);

            await _emailService.SendEmailAsync(
                Email,
                "Password Reset Request",
                $@"<h3>Hello {user.FullName},</h3>
                <p>You requested to reset your password.</p>
                <p><a href='{resetUrl}'>Click here to reset it</a></p>
                <p>This link is valid for 10 minutes.</p>");

            Message = "A reset link has been sent to your email.";
            return Page();
        }
    }
}
