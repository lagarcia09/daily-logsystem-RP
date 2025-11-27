using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DailyLogSystem.Models;
using DailyLogSystem.Services;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace DailyLogSystem.Pages
{
    [AllowAnonymous]
    public class IndexModel : PageModel
    {
        private readonly MongoDbService _mongoService;
        private readonly EmailService _emailService;
        private readonly ITokenStore _tokenStore;

        public IndexModel(MongoDbService mongoService, EmailService emailService, ITokenStore tokenStore)
        {
            _mongoService = mongoService;
            _emailService = emailService;
            _tokenStore = tokenStore;
        }

        // USER INPUTS
        [BindProperty]
        public Employee EmployeeInput { get; set; } = new();

        [BindProperty]
        public Admin AdminInput { get; set; } = new();

        public string? DeactivatedMessage { get; set; }

        public string? ErrorMessage { get; set; }
        public string? Message { get; set; }


        // ===================== EMPLOYEE LOGIN =====================
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
                ErrorMessage = "Your account is deactivated. Please contact admin.";
                return Page();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, employee.Email),
                new Claim(ClaimTypes.Role, "User"),
                new Claim("EmployeeId", employee.EmployeeId)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            HttpContext.Session.SetString("UserEmployeeId", employee.EmployeeId);

            // OPTIONAL: Send login email
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


        // ===================== ADMIN LOGIN =====================
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

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, admin.Email),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("AdminId", admin.AdminId)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            HttpContext.Session.SetString("AdminId", admin.AdminId);
            HttpContext.Session.SetString("AdminName", admin.FullName);

            // OPTIONAL: Send login email
            var subject = "Login Notification - Daily Log System";
            var body = $@"
                <h3>Hi {admin.FullName},</h3>
                <p>You have successfully logged in to the Daily Log System.</p>
                <p><a href='https://localhost:7102/AdminOverview'>Go to Dashboard</a></p>
                <p>– Daily Log System Team</p>";
            _emailService.SendEmail(admin.Email, subject, body);

            return RedirectToPage("/IndexSuccess");
        }


        // ===================== TOKEN-BASED LOGIN HANDLER =====================
        public async Task<IActionResult> OnGetAsync(string? token)
        {
            HttpContext.Session.Clear();

            if (string.IsNullOrWhiteSpace(token))
                return Page();

            var record = _tokenStore.Get(token);

            if (record == null || record.IsUsed || record.Expiry < DateTime.UtcNow)
                return RedirectToPage("/LoginExpired");

            _tokenStore.MarkUsed(token);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, record.Email),
                new Claim(ClaimTypes.Role, record.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            return RedirectToPage(record.Role == "Admin"
                ? "/AdminOverview"
                : "/Dashboard");
        }
    }
}
