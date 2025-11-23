using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DailyLogSystem.Models;
using DailyLogSystem.Services;
using System.Threading.Tasks;

namespace DailyLogSystem.Pages
{
    public class IndexModel : PageModel
    {
        private readonly MongoDbService _mongoService;
        private readonly EmailService _emailService;

        [BindProperty]
        public Employee EmployeeInput { get; set; } = new();

        [BindProperty]
        public Admin AdminInput { get; set; } = new();
        public string? DeactivatedMessage { get; set; }

        public string? ErrorMessage { get; set; }

        public IndexModel(MongoDbService mongoService, EmailService emailService)
        {
            _mongoService = mongoService;
            _emailService = emailService;
        }

        public void OnGet()
        {
            
            HttpContext.Session.Clear();
        }

        // emplyoee login dito

        public async Task<IActionResult> OnPostEmployeeLoginAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var empId = EmployeeInput.EmployeeId?.Trim();
            var password = EmployeeInput.Password?.Trim();

            if (string.IsNullOrEmpty(empId) || string.IsNullOrEmpty(password))
            {
                ErrorMessage = "Please enter both Employee ID and Password.";
                return Page();
            }


            var employee = await _mongoService.GetByEmployeeIdAsync(empId);

            if (employee == null)
            {
                ErrorMessage = "Invalid Employee ID.";
                return Page();
            }

            if (employee.Password.Trim() != password)
            {
                ErrorMessage = "Incorrect password.";
                return Page();
            }
            if (!employee.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Your account is deactivated. Please contact admin.");
                return Page();
            }




            HttpContext.Session.SetString("UserEmployeeId", employee.EmployeeId);

            //email notif to login
            var subject = "Login Notification - Daily Log System";
            var body = $@"
                <h3>Hi {employee.FullName},</h3>
                <p>You have successfully logged in to the Daily Log System.</p>
                <p><a href='https://localhost:7102/Dashboard'>Go to Dashboard</a></p>
                <p>– Daily Log System Team</p>";

            _emailService.SendEmail(employee.Email, subject, body);

            TempData["SuccessMessage"] = "Login successful!";
            return RedirectToPage("/IndexSuccess");
        }

        //admin login
        public async Task<IActionResult> OnPostAdminLoginAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var adminId = AdminInput.AdminId?.Trim();
            var password = AdminInput.Password?.Trim();

            if (string.IsNullOrEmpty(adminId) || string.IsNullOrEmpty(password))
            {
                ErrorMessage = "Please enter both Admin ID and Password.";
                return Page();
            }

            var admin = await _mongoService.GetAdminByIdAsync(adminId);

            if (admin == null || admin.Password != password)
            {
                ErrorMessage = "Invalid Admin ID or Password.";
                return Page();
            }

          
            HttpContext.Session.SetString("AdminId", admin.AdminId);
            HttpContext.Session.SetString("AdminName", admin.FullName);

            var subject = "Login Notification - Daily Log System";
            var body = $@"
                <h3>Hi {admin.FullName},</h3>
                <p>You have successfully logged in to the Daily Log System.</p>
                <p><a href='https://localhost:7102/AdminOverview'>Go to Dashboard</a></p>
                <p>– Daily Log System Team</p>";

            _emailService.SendEmail(admin.Email, subject, body);

           
            return RedirectToPage("/IndexSuccess");
        }
    }
}
