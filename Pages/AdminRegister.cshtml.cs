using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DailyLogSystem.Models;
using DailyLogSystem.Services;
using System;
using System.Threading.Tasks;

namespace DailyLogSystem.Pages
{
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

            // ✅ Generate unique Admin ID (format: ADM-YY-XXXXX)
            string year = DateTime.Now.Year.ToString().Substring(2, 2);
            string randomDigits = new Random().Next(10000, 99999).ToString();
            Input.AdminId = $"ADM-{year}-{randomDigits}";

            // ✅ Normalize input
            Input.Email = Input.Email.Trim().ToLower();
            Input.Password = Input.Password.Trim();
            Input.FullName = Input.FullName.Trim();

            // ✅ Save to MongoDB (Admins collection)
            await _mongoService.CreateAdminAsync(Input);

            // ✅ Send confirmation email
            var subject = "Admin Registration Successful - Daily Log System";
            var body = $@"
                <h3>Hi {Input.FullName},</h3>
                <p>You have been successfully registered as an <strong>Administrator</strong> in the Daily Log System.</p>
                <p>Your Admin ID is: <strong>{Input.AdminId}</strong></p>
                <p>You can now log in using this Admin ID and your password.</p>
                <br/>
                <p>– Daily Log System Team</p>";

            _emailService.SendEmail(Input.Email, subject, body);

            return RedirectToPage("RegisterSuccess");
        }
    }
}
