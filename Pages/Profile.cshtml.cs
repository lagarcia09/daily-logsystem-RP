using DailyLogSystem.Models;
using DailyLogSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DailyLogSystem.Pages
{
    public class ProfileModel : PageModel
    {
        private readonly MongoDbService _mongo;

        public ProfileModel(MongoDbService mongo)
        {
            _mongo = mongo;
        }

        [BindProperty]
        public string FullName { get; set; } = "";

        [BindProperty]
        public string ContactNumber { get; set; } = "";

        [BindProperty]
        public string Address { get; set; } = "";

        public async Task<IActionResult> OnGetAsync()
        {
            var empId = HttpContext.Session.GetString("UserEmployeeId");
            if (empId == null)
                return RedirectToPage("/Index");

            var emp = await _mongo.GetByEmployeeIdAsync(empId);
            if (emp == null)
                return RedirectToPage("/Index");

            FullName = emp.FullName;
            ContactNumber = emp.ContactNumber;
            Address = emp.Address;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var empId = HttpContext.Session.GetString("UserEmployeeId");
            if (empId == null)
                return RedirectToPage("/Index");

            await _mongo.UpdateProfileAsync(empId, FullName, ContactNumber, Address);

            TempData["Success"] = "Profile updated successfully!";
            return RedirectToPage("/Profile");
        }
    }
}
