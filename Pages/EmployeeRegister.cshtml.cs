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
    public class EmployeeRegisterModel : PageModel
    {
        private readonly MongoDbService _mongoService;
        private readonly EmailService _emailService;

        [BindProperty]
        public Employee Input { get; set; } = new();

        public EmployeeRegisterModel(MongoDbService mongoService, EmailService emailService)
        {
            _mongoService = mongoService ?? throw new ArgumentNullException(nameof(mongoService));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        }

       

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            // generate emp id
            string year = DateTime.Now.Year.ToString().Substring(2, 2);
            string randomDigits = new Random().Next(10000, 99999).ToString();
            Input.EmployeeId = $"{year}-{randomDigits}";


            Input.Email = Input.Email!.Trim().ToLower();
            Input.Password = Input.Password!.Trim();
            Input.FullName = Input.FullName!.Trim();




            await _mongoService.AddEmployeeAsync(Input);

            // email notif success/ login
            var subject = "Registration Successful - Daily Log System";
            var body = $@"
                <h3>Hi {Input.FullName},</h3>
                <p>Thank you for registering to the Daily Log System.</p>
                <p>Your Employee ID is: <strong>{Input.EmployeeId}</strong></p>
                <p>You can now log in using this Employee ID and your password.</p>
                <br/>
                <p>– Daily Log System Team</p>";

            _emailService.SendEmail(Input.Email, subject, body);

            return RedirectToPage("RegisterSuccess");
        }
    }
}
