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
            if (emp == null)
                return;

            EmployeeId = emp.EmployeeId;
            EmployeeName = emp.FullName;

            TodayRecord = await _mongoService.GetTodayRecordAsync(emp.EmployeeId);

            if (TodayRecord == null)
                return; // Prevents null dereference

            // ---------- TIMEZONE FIX ----------
            var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

            TodayRecord.Date = TimeZoneInfo.ConvertTimeFromUtc(TodayRecord.Date, phTimeZone);

            if (TodayRecord.TimeIn.HasValue)
                TodayRecord.TimeIn = TimeZoneInfo.ConvertTimeFromUtc(TodayRecord.TimeIn.Value, phTimeZone);

            if (TodayRecord.TimeOut.HasValue)
                TodayRecord.TimeOut = TimeZoneInfo.ConvertTimeFromUtc(TodayRecord.TimeOut.Value, phTimeZone);

            // ---------- STATUS CALCULATION ----------
            var officeStart = TodayRecord.Date.Date.AddHours(8); // 8 AM

            if (TodayRecord.TimeIn.HasValue)
            {
                TodayRecord.Status =
                    TodayRecord.TimeIn.Value > officeStart ? "LATE" : "ON TIME";
            }
            else
            {
                TodayRecord.Status = "ABSENT";
            }

            // ---------- SAFELY COMPUTE TOTAL HOURS ----------
            if (TodayRecord.TimeIn.HasValue && TodayRecord.TimeOut.HasValue)
            {
                var total = TodayRecord.TimeOut.Value - TodayRecord.TimeIn.Value;
                TodayRecord.TotalHours = total.TotalHours.ToString("0.00");

                var overtimeStart = TodayRecord.Date.Date.AddHours(17); // 5 PM

                if (TodayRecord.TimeOut.Value > overtimeStart)
                {
                    var overtime = TodayRecord.TimeOut.Value - overtimeStart;
                    TodayRecord.OvertimeHours = overtime.TotalHours.ToString("0.00");
                }
                else
                {
                    TodayRecord.OvertimeHours = "0";
                }
            }
            else
            {
                TodayRecord.TotalHours = "0";
                TodayRecord.OvertimeHours = "0";
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

        public async Task<IActionResult> OnPostUploadDocsAsync(List<IFormFile> files)
        {
            var currentUserId = HttpContext.Session.GetString("UserEmployeeId");
            if (string.IsNullOrEmpty(currentUserId))
                return RedirectToPage("/Index");

            var uploadPath = Path.Combine("wwwroot", "docs", currentUserId, DateTime.Now.ToString("yyyy-MM-dd"));

            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            foreach (var file in files)
            {
                var filePath = Path.Combine(uploadPath, file.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
            }

            TempData["Message"] = "Documents uploaded successfully!";
            return RedirectToPage();
        }

    }
}
