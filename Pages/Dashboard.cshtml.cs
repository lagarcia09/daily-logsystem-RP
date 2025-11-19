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

            // ❌ Not logged in or session expired
            if (string.IsNullOrEmpty(currentUserId))
            {
                HttpContext.Session.Clear();
                Response.Redirect("/Index");
                return;
            }

            // ❌ Prevent accessing another user's dashboard via browser link
            var emp = await _mongoService.GetByEmployeeIdAsync(currentUserId);

            if (emp == null)
            {
                HttpContext.Session.Clear();
                Response.Redirect("/Index");
                return;
            }


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

            
            // ---------- STATUS + HOURS BASED ON NEW RULES ----------

            var officeStart = TodayRecord.Date.Date.AddHours(8); // 8 AM
            var officeEnd = TodayRecord.Date.Date.AddHours(17);  // 5 PM

            // No Time-In = absent
            if (!TodayRecord.TimeIn.HasValue)
            {
                TodayRecord.Status = "ABSENT";
                TodayRecord.TotalHours = "0";
                TodayRecord.OvertimeHours = "0";
            }
            else
            {
                var timeIn = TodayRecord.TimeIn.Value;

                // (1) CHECK 2-HOUR LATE = ABSENT RULE
                var lateDuration = timeIn - officeStart;

                if (lateDuration.TotalHours >= 2)
                {
                    TodayRecord.Status = "ABSENT";
                    TodayRecord.TotalHours = "0";
                    TodayRecord.OvertimeHours = "0";
                }
                else
                {
                    // NORMAL CASE (Not 2 hours late)
                    if (timeIn > officeStart)
                        TodayRecord.Status = "LATE";
                    else
                        TodayRecord.Status = "ON TIME";

                    // If Time-Out exists, compute hours
                    if (TodayRecord.TimeOut.HasValue)
                    {
                        var timeOut = TodayRecord.TimeOut.Value;

                        // Compute total worked hours
                        var total = timeOut - timeIn;
                        TodayRecord.TotalHours = total.TotalHours.ToString("0.00");

                        // (A) OVERTIME — TimeOut AFTER 5:00 PM
                        if (timeOut > officeEnd)
                        {
                            var overtime = timeOut - officeEnd;
                            TodayRecord.OvertimeHours = overtime.TotalHours.ToString("0.00");
                            TodayRecord.UndertimeHours = "0";
                            TodayRecord.Status = "OVERTIME";
                        }
                        // (B) UNDERTIME — TimeOut BEFORE 5:00 PM
                        else if (timeOut < officeEnd)
                        {
                            var undertime = officeEnd - timeOut;
                            TodayRecord.UndertimeHours = undertime.TotalHours.ToString("0.00");
                            TodayRecord.OvertimeHours = "0";
                            TodayRecord.Status = "UNDERTIME";
                        }
                        // (C) EXACT 5:00 PM — No overtime or undertime
                        else
                        {
                            TodayRecord.OvertimeHours = "0";
                            TodayRecord.UndertimeHours = "0";
                            // Keep ON TIME / LATE status from time-in
                        }
                    }
                    else
                    {
                        // No TimeOut yet
                        TodayRecord.TotalHours = "0";
                        TodayRecord.OvertimeHours = "0";
                        TodayRecord.UndertimeHours = "0";
                    }
                }
            }

            // ✅ Save computed status/hours back to MongoDB
            await _mongoService.UpdateTodayRecordStatusAsync(emp.EmployeeId, TodayRecord);

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
