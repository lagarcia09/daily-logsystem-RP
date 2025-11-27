using DailyLogSystem.Models;
using DailyLogSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DailyLogSystem.Pages
{
    public class AdminEmployeesModel : PageModel
    {
        private readonly MongoDbService _mongoService;
        private readonly EmailService _emailService;

        public List<Employee> Employees { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string Search { get; set; } = string.Empty;

        public AdminEmployeesModel(MongoDbService mongoService, EmailService emailService)
        {
            _mongoService = mongoService;
            _emailService = emailService;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var adminId = HttpContext.Session.GetString("AdminId");
            if (string.IsNullOrEmpty(adminId))
            {
                return RedirectToPage("/Index");
            }

            var allEmployees = await _mongoService.GetAllEmployeesAsync();

            if (!string.IsNullOrWhiteSpace(Search))
            {
                string lower = Search.ToLower();

                Employees = allEmployees.Where(e =>
                    (e.EmployeeId ?? "").ToLower().Contains(lower) ||
                    (e.FullName ?? "").ToLower().Contains(lower) ||
                    (e.Email ?? "").ToLower().Contains(lower) ||
                    (e.Position ?? "").ToLower().Contains(lower) // Supports search by position
                ).ToList();
            }
            else
            {
                Employees = allEmployees;
            }

            return Page();
        }


        // Add Employee using Position
        public async Task<IActionResult> OnPostAddEmployeeAsync(string FullName, string Email, string Position)
        {
            var newEmp = new Employee
            {
                FullName = FullName?.Trim() ?? string.Empty,
                Email = Email?.Trim().ToLower() ?? string.Empty,
                Position = Position?.Trim() ?? string.Empty,
                IsActive = true,
                EmployeeId = GenerateEmployeeId()
            };

            await _mongoService.AddEmployeeAsync(newEmp);
            return RedirectToPage();
        }


        public async Task<IActionResult> OnPostDeactivateEmployeeAsync(string id)
        {
            await _mongoService.DeactivateEmployeeAsync(id);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostReactivateEmployeeAsync(string id)
        {
            await _mongoService.ReactivateEmployeeAsync(id);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostResetPasswordAsync(string id, string NewPassword)
        {
            if (string.IsNullOrWhiteSpace(NewPassword))
                return RedirectToPage();

            var emp = await _mongoService.ResetEmployeePasswordAsync(id, NewPassword);

            if (emp != null && !string.IsNullOrEmpty(emp.Email))
            {
                await _emailService.SendEmailAsync(
                    emp.Email,
                    "Password Reset Notification",
                    $"Hi {emp.FullName},<br/><br/>" +
                    $"Your password has been reset by the admin.<br/>" +
                    $"Your new password is: <b>{NewPassword}</b><br/><br/>" +
                    $"Please keep it secure."
                );
            }

            return RedirectToPage();
        }

        private string GenerateEmployeeId()
        {
            var rand = new Random();
            return $"25-{rand.Next(10000, 99999)}";
        }
    }
}
