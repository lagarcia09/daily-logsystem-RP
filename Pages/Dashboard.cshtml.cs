using DailyLogSystem.Models;
using DailyLogSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DailyLogSystem.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly MongoDbService _mongoService;

        [BindProperty]
        public string EmployeeId { get; set; } = "";

        [BindProperty]
        public string EmployeeName { get; set; } = "";

        public TodayRecord? TodayRecord { get; set; }

        public DashboardModel(MongoDbService mongoService)
        {
            _mongoService = mongoService;
        }

        public async Task OnGetAsync()
        {
            var currentUserId = HttpContext.Session.GetString("UserEmployeeId");

            if (string.IsNullOrEmpty(currentUserId))
            {
                Response.Redirect("/Index");
                return;
            }

            var emp = await _mongoService.GetByEmployeeIdAsync(currentUserId);
            if (emp != null)
            {
                EmployeeId = emp.EmployeeId;
                EmployeeName = emp.FullName;

                TodayRecord = await _mongoService.GetTodayRecordAsync(emp.EmployeeId);

                if (TodayRecord != null)
                {
                    var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
                    TodayRecord.Date = TimeZoneInfo.ConvertTimeFromUtc(TodayRecord.Date, phTimeZone);

                    if (TodayRecord.TimeIn.HasValue)
                        TodayRecord.TimeIn = TimeZoneInfo.ConvertTimeFromUtc(TodayRecord.TimeIn.Value, phTimeZone);

                    if (TodayRecord.TimeOut.HasValue)
                        TodayRecord.TimeOut = TimeZoneInfo.ConvertTimeFromUtc(TodayRecord.TimeOut.Value, phTimeZone);
                }
            }
        }

        public async Task<IActionResult> OnPostAsync(string action)
        {
            // ✅ Use correct session variable
            var currentUserId = HttpContext.Session.GetString("UserEmployeeId");
            if (string.IsNullOrEmpty(currentUserId))
                return RedirectToPage("/Index");

            var emp = await _mongoService.GetByEmployeeIdAsync(currentUserId);
            if (emp == null)
                return RedirectToPage("/Index");

            var phTime = GetPhilippineTime();

            // ✅ Record attendance
            if (action == "TimeIn")
                await _mongoService.RecordTimeInAsync(emp.EmployeeId, phTime);
            else if (action == "TimeOut")
                await _mongoService.RecordTimeOutAsync(emp.EmployeeId, phTime);

            // ✅ Refresh the same page (stay on dashboard)
            return RedirectToPage();
        }

        private DateTime GetPhilippineTime()
        {
            var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);
        }
    }
}
