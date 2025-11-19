using DailyLogSystem.Models;
using DailyLogSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DailyLogSystem.Pages
{
    public class AdminAttendanceModel : PageModel
    {
        private readonly MongoDbService _mongoService;

        public List<TodayRecord> AttendanceLogs { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? SearchName { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        public AdminAttendanceModel(MongoDbService mongoService)
        {
            _mongoService = mongoService;
        }

        public async Task OnGetAsync()
        {
            AttendanceLogs = await _mongoService.GetAllRecordsAsync();

            // ✅ SAFE SEARCH FILTER
            if (!string.IsNullOrWhiteSpace(SearchName))
            {
                string search = SearchName ?? ""; // null-safe
                AttendanceLogs = AttendanceLogs
                    .Where(l => (l.EmployeeName ?? "").Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // ✅ SAFE STATUS FILTER
            if (!string.IsNullOrWhiteSpace(StatusFilter))
            {
                string status = StatusFilter ?? ""; // null-safe
                AttendanceLogs = AttendanceLogs
                    .Where(l => (l.Status ?? "") == status)
                    .ToList();
            }
        }

        public async Task<IActionResult> OnPostOverrideLogAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
                return Page(); // avoid null id error

            await _mongoService.OverrideAttendanceLogAsync(id);
            return RedirectToPage();
        }
    }
}
