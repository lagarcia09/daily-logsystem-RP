using DailyLogSystem.Models;
using DailyLogSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DailyLogSystem.Pages
{
    public class AdminOverviewModel : PageModel
    {
        private readonly MongoDbService _mongoService;

        public int TotalEmployees { get; set; }
        public int PresentToday { get; set; }
        public int AbsentToday { get; set; }
        public int LateToday { get; set; }
        public int UndertimeToday { get; set; }
        public int OvertimeToday { get; set; }

        public List<Employee> Employees { get; set; } = new();
        public List<TodayRecord> AttendanceLogs { get; set; } = new();

        public AdminOverviewModel(MongoDbService mongoService)
        {
            _mongoService = mongoService;
        }

        public async Task<IActionResult> OnGet()
        {
            // CHECK IF ADMIN IS LOGGED IN
            var adminId = HttpContext.Session.GetString("AdminId");

            if (string.IsNullOrEmpty(adminId))
            {
                // Not logged in → redirect to login
                return RedirectToPage("/Index");
            }

            Employees = await _mongoService.GetAllEmployeesAsync();
            TotalEmployees = Employees.Count;

            AttendanceLogs = await _mongoService.GetAllRecordsAsync();

            // PH TIMEZONE
            var phZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            var phToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phZone).Date;

            // Filter using PH date
            var todayLogs = AttendanceLogs.Where(l =>
            {
                var phDate = TimeZoneInfo.ConvertTimeFromUtc(l.Date, phZone).Date;
                return phDate == phToday;
            }).ToList();

            // ---- COUNT STATUSES ----
            AbsentToday = todayLogs.Count(l => l.Status?.ToUpper() == "ABSENT");

            // Present = Present + Late + Undertime + Overtime
            PresentToday = todayLogs.Count(l =>
            {
                var status = l.Status?.ToUpper();
                bool isLate = status == "LATE";
                bool isPresent = status == "PRESENT";

                bool hasUndertime = !string.IsNullOrEmpty(l.UndertimeHours) &&
                                    double.TryParse(l.UndertimeHours, out var ut) &&
                                    ut > 0;

                bool hasOvertime = double.TryParse(l.OvertimeHours, out var ot) &&
                                   ot > 0;

                return isPresent || isLate || hasUndertime || hasOvertime;
            });

            LateToday = todayLogs.Count(l => l.Status?.ToUpper() == "LATE");

            UndertimeToday = todayLogs.Count(l =>
                !string.IsNullOrEmpty(l.UndertimeHours) &&
                double.TryParse(l.UndertimeHours, out var ut) &&
                ut > 0
            );

            OvertimeToday = todayLogs.Count(l =>
                double.TryParse(l.OvertimeHours, out var ot) &&
                ot > 0
            );

            return Page();
        }
      }
    }
