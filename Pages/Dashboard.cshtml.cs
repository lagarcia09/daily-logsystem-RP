using DailyLogSystem.Models;
using DailyLogSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DailyLogSystem.Pages
{
    [Authorize(Roles = "User")]   // ✅ Added token role authorization
    public class DashboardModel : PageModel
    {
        private readonly MongoDbService _mongoService;

        [BindProperty]
        public string EmployeeId { get; set; } = "";

        [BindProperty]
        public string FullName { get; set; } = "";

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
                HttpContext.Session.Clear();
                Response.Redirect("/Index");
                return;
            }

            var emp = await _mongoService.GetByEmployeeIdAsync(currentUserId);
            if (emp == null)
            {
                HttpContext.Session.Clear();
                Response.Redirect("/Index");
                return;
            }

            EmployeeId = emp.EmployeeId;
            FullName = emp.FullName;

            TodayRecord = await _mongoService.GetTodayRecordAsync(emp.EmployeeId);

            if (TodayRecord != null)
            {
                var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

                TodayRecord.Date = TimeZoneInfo.ConvertTimeFromUtc(TodayRecord.Date, phTimeZone);
                if (TodayRecord.TimeIn.HasValue)
                    TodayRecord.TimeIn = TimeZoneInfo.ConvertTimeFromUtc(TodayRecord.TimeIn.Value, phTimeZone);
                if (TodayRecord.TimeOut.HasValue)
                    TodayRecord.TimeOut =
                        TimeZoneInfo.ConvertTimeFromUtc(TodayRecord.TimeOut.Value, phTimeZone);

                var officeStart = TodayRecord.Date.Date.AddHours(8);
                var officeEnd = TodayRecord.Date.Date.AddHours(17);

                if (!TodayRecord.TimeIn.HasValue)
                {
                    TodayRecord.Status = "ABSENT";
                    TodayRecord.TotalHours = "0";
                    TodayRecord.OvertimeHours = "0";
                }
                else
                {
                    var timeIn = TodayRecord.TimeIn.Value;

                    // Normalize both times to ignore seconds
                    timeIn = new DateTime(timeIn.Year, timeIn.Month, timeIn.Day, timeIn.Hour, timeIn.Minute, 0);
                    var allowedLatestOnTime = officeStart.AddMinutes(1); // 8:01 AM cutoff

                    var lateDuration = timeIn - officeStart;

                    // ABSENT if 2 hours late or more
                    if (lateDuration.TotalHours >= 2)
                    {
                        TodayRecord.Status = "ABSENT";
                        TodayRecord.TotalHours = "0";
                        TodayRecord.OvertimeHours = "0";
                    }
                    else
                    {
                        // ON TIME if before 8:01
                        if (timeIn < allowedLatestOnTime)
                        {
                            TodayRecord.Status = "ON TIME";
                        }
                        else
                        {
                            TodayRecord.Status = "LATE";
                        }
                    }


                    if (TodayRecord.TimeOut.HasValue)
                    {
                        var timeOut = TodayRecord.TimeOut.Value;
                        var total = timeOut - timeIn;
                        TodayRecord.TotalHours = total.TotalHours.ToString("0.00");

                        if (timeOut > officeEnd)
                        {
                            TodayRecord.OvertimeHours = (timeOut - officeEnd).TotalHours.ToString("0.00");
                            TodayRecord.UndertimeHours = "0";
                            TodayRecord.Status = "OVERTIME";
                        }
                        else if (timeOut < officeEnd)
                        {
                            TodayRecord.UndertimeHours = (officeEnd - timeOut).TotalHours.ToString("0.00");
                            TodayRecord.OvertimeHours = "0";
                            TodayRecord.Status = "UNDERTIME";
                        }
                        else
                        {
                            TodayRecord.OvertimeHours = "0";
                            TodayRecord.UndertimeHours = "0";
                        }
                    }
                    else
                    {
                        TodayRecord.TotalHours = "0";
                        TodayRecord.OvertimeHours = "0";
                        TodayRecord.UndertimeHours = "0";
                    }
                }
            }

            if (TodayRecord != null)
            {
                await _mongoService.UpdateTodayRecordStatusAsync(emp.EmployeeId, TodayRecord);
            }

        }


        public async Task<IActionResult> OnPostAsync(string action)
        {
            var currentUserId = HttpContext.Session.GetString("UserEmployeeId");
            if (string.IsNullOrEmpty(currentUserId))
                return RedirectToPage("/Index");

            var emp = await _mongoService.GetByEmployeeIdAsync(currentUserId);
            if (emp == null)
                return RedirectToPage("/Index");

            var phTime = GetPhilippineTime();

            if (action == "TimeIn")
                await _mongoService.RecordTimeInAsync(emp.EmployeeId, phTime);
            else if (action == "TimeOut")
                await _mongoService.RecordTimeOutAsync(emp.EmployeeId, phTime);

            return RedirectToPage();
        }

        private DateTime GetPhilippineTime()
        {
            var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);
        }
    }
}
