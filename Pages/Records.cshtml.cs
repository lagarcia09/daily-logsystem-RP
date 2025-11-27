using DailyLogSystem.Models;
using DailyLogSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace DailyLogSystem.Pages
{
    public class RecordsModel : PageModel
    {
        private readonly MongoDbService _mongoService;
        private readonly IPdfService _pdfService;

        public List<TodayRecord> Logs { get; set; } = new();
        public int TotalWorkedDays { get; set; }
        public double TotalHoursThisMonth { get; set; }
        public double OvertimeThisMonth { get; set; }
        public double UndertimeThisMonth { get; set; }
        public int AbsencesThisMonth { get; set; }
        public double AttendanceRate { get; set; }

        public string PunctualityJson { get; set; } = "";
        public string WorkHoursJson { get; set; } = "";

        public RecordsModel(MongoDbService mongo, IPdfService pdfService)
        {
            _mongoService = mongo;
            _pdfService = pdfService;
        }

        public async Task OnGetAsync()
        {
            var id = HttpContext.Session.GetString("UserEmployeeId");
            if (id == null)
            {
                Response.Redirect("/Index");
                return;
            }

            Logs = await _mongoService.GetAllLogsAsync(id);

            var ph = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

            foreach (var l in Logs)
            {
                l.Date = TimeZoneInfo.ConvertTimeFromUtc(l.Date, ph);

                if (l.TimeIn.HasValue)
                    l.TimeIn = TimeZoneInfo.ConvertTimeFromUtc(l.TimeIn.Value, ph);

                if (l.TimeOut.HasValue)
                    l.TimeOut = TimeZoneInfo.ConvertTimeFromUtc(l.TimeOut.Value, ph);
            }

            var now = DateTime.Now;

            var currentMonthLogs = Logs
                .Where(l => l.Date.Month == now.Month && l.Date.Year == now.Year)
                .ToList();

            TotalWorkedDays = currentMonthLogs.Count(l => l.TimeIn.HasValue && l.Status != "ABSENT");

            int totalWorkingDays = Enumerable.Range(1, DateTime.DaysInMonth(now.Year, now.Month))
                .Select(day => new DateTime(now.Year, now.Month, day))
                .Where(d => d <= now.Date)
                .Where(d => d.DayOfWeek != DayOfWeek.Saturday &&
                            d.DayOfWeek != DayOfWeek.Sunday)
                .Count();

            AbsencesThisMonth = currentMonthLogs.Count(l => l.Status == "ABSENT" || (!l.TimeIn.HasValue && !l.TimeOut.HasValue));
            AbsencesThisMonth += Math.Max(0, totalWorkingDays - currentMonthLogs.Count);

            foreach (var day in Enumerable.Range(1, DateTime.DaysInMonth(now.Year, now.Month)))
            {
                var date = new DateTime(now.Year, now.Month, day);

                if (date > now.Date) continue;
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

                bool hasRecord = currentMonthLogs.Any(l => l.Date.Date == date.Date);

                if (!hasRecord)
                {
                    Logs.Add(new TodayRecord
                    {
                        Date = date,
                        TotalHours = "0h 0m",
                        TimeIn = null,
                        TimeOut = null,
                        Status = "ABSENT"
                    });
                }
            }

            Logs = Logs.OrderBy(l => l.Date).ToList();
            currentMonthLogs = Logs.Where(l => l.Date.Month == now.Month).ToList();

            TotalHoursThisMonth = 0;
            OvertimeThisMonth = 0;
            UndertimeThisMonth = 0;

            foreach (var l in currentMonthLogs)
            {
                if (l.Status == "ABSENT") continue;

                if (l.TimeIn.HasValue && l.TimeOut.HasValue)
                {
                    var diff = l.TimeOut.Value - l.TimeIn.Value;
                    TotalHoursThisMonth += diff.TotalHours;

                    var officeEnd = l.Date.Date.AddHours(17);

                    if (l.TimeOut.Value > officeEnd)
                        OvertimeThisMonth += (l.TimeOut.Value - officeEnd).TotalHours;

                    if (l.TimeOut.Value < officeEnd)
                        UndertimeThisMonth += (officeEnd - l.TimeOut.Value).TotalHours;
                }
            }

            TotalHoursThisMonth = Math.Round(TotalHoursThisMonth, 2);
            OvertimeThisMonth = Math.Round(OvertimeThisMonth, 2);
            UndertimeThisMonth = Math.Round(UndertimeThisMonth, 2);

            AttendanceRate = totalWorkingDays > 0
                ? Math.Round(((double)TotalWorkedDays / totalWorkingDays) * 100, 2)
                : 0;

            PunctualityJson = JsonSerializer.Serialize(new
            {
                labels = new[] { "Worked Days", "Absences" },
                datasets = new[]
                {
                    new {
                        label = "Attendance",
                        data = new[] { TotalWorkedDays, AbsencesThisMonth },
                        backgroundColor = new[] { "#5D866C", "#C2A68C" }
                    }
                }
            });

            WorkHoursJson = JsonSerializer.Serialize(new
            {
                labels = new[] { "Regular Hours", "Overtime", "Undertime" },
                datasets = new[]
                {
                    new {
                        label = "Hours",
                        data = new[] { TotalHoursThisMonth, OvertimeThisMonth, UndertimeThisMonth },
                        backgroundColor = new[] { "#5D866C", "#C2A68C", "#E6D8C3" }
                    }
                }
            });

            // FIXED: This block must be INSIDE OnGetAsync()
            Logs = Logs.Select(l =>
            {
                if (l.Status == "ABSENT")
                {
                    l.TimeIn = null;
                    l.TimeOut = null;
                    l.TotalHours = "0h 0m";
                }
                return l;
            }).ToList();
        }
        // <-- OnGetAsync now ends correctly HERE ✔✔✔

        public async Task<IActionResult> OnPostExportPDFAsync()
        {
            var id = HttpContext.Session.GetString("UserEmployeeId");
            if (id == null) return RedirectToPage("/Index");

            var employee = await _mongoService.GetByEmployeeIdAsync(id);
            if (employee == null) return RedirectToPage("/Index");

            var logs = await _mongoService.GetAllLogsAsync(id);

            var now = DateTime.Now;
            var pdf = await _pdfService.GenerateMonthlyReportAsync(employee, logs, now.Month, now.Year);

            return File(pdf, "application/pdf", $"MonthlyReport_{employee.EmployeeId}_{now:yyyyMM}.pdf");
        }

        public async Task<IActionResult> OnPostExportDailyAsync(string date)
        {
            var id = HttpContext.Session.GetString("UserEmployeeId");
            if (id == null) return RedirectToPage("/Index");

            var employee = await _mongoService.GetByEmployeeIdAsync(id);
            if (employee == null) return RedirectToPage("/Index");

            if (!DateTime.TryParse(date, out var dt)) return BadRequest("Invalid date");

            var logs = await _mongoService.GetAllLogsAsync(id);
            var record = logs.FirstOrDefault(l => l.Date.Date == dt.Date);

            if (record == null) return NotFound();

            var pdf = await _pdfService.GenerateDailyReportAsync(employee, record);
            return File(pdf, "application/pdf", $"DailyReport_{employee.EmployeeId}_{dt:yyyyMMdd}.pdf");
        }
    }
}
