using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DailyLogSystem.Models;
using DailyLogSystem.Services;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace DailyLogSystem.Pages
{
    [AllowAnonymous]
    public class AdminRegisterModel : PageModel
    {
        private readonly MongoDbService _mongoService;
        private readonly EmailService _emailService;

        [BindProperty]
        public Admin Input { get; set; } = new();

        public AdminRegisterModel(MongoDbService mongoService, EmailService emailService)
        {
            _mongoService = mongoService ?? throw new ArgumentNullException(nameof(mongoService));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            // Generate Admin ID (ADM-YY-XXXXX)
            string year = DateTime.Now.Year.ToString().Substring(2, 2);
            string randomDigits = new Random().Next(10000, 99999).ToString();
            Input.AdminId = $"ADM-{year}-{randomDigits}";

            // Clean input
            Input.Email = Input.Email.Trim().ToLower();
            Input.Password = Input.Password.Trim();
            Input.FullName = Input.FullName.Trim();

            // Save to MongoDB
            await _mongoService.CreateAdminAsync(Input);

            // Email notification
            var subject = "Admin Registration Successful - Daily Log System";
            var body = $@"
                <h3>Hi {Input.FullName},</h3>
                <p>You have been successfully registered as an <strong>Administrator</strong>.</p>
                <p>Your Admin ID: <strong>{Input.AdminId}</strong></p>";

            _emailService.SendEmail(Input.Email, subject, body);

            return RedirectToPage("RegisterSuccess");
        }
    }
}
