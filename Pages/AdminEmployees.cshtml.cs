using DailyLogSystem.Models;
using DailyLogSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DailyLogSystem.Pages
{
    public class AdminEmployeesModel : PageModel
    {
        private readonly MongoDbService _mongoService;

        public List<Employee> Employees { get; set; } = new();

        public AdminEmployeesModel(MongoDbService mongoService)
        {
            _mongoService = mongoService;
        }

        public async Task OnGetAsync()
        {
            Employees = await _mongoService.GetAllEmployeesAsync();
        }

        public async Task<IActionResult> OnPostAddEmployeeAsync(string FullName, string Email, string Role)
        {
            var newEmp = new Employee
            {
                FullName = FullName,
                Email = Email,
                Role = Role,
                IsActive = true
            };

            await _mongoService.AddEmployeeAsync(newEmp);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeactivateEmployeeAsync(string id)
        {
            await _mongoService.DeactivateEmployeeAsync(id);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostResetPasswordAsync(string id)
        {
            await _mongoService.ResetEmployeePasswordAsync(id);
            return RedirectToPage();
        }
    }
}
