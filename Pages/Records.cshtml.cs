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

            // Fetch all logs for the user
            Logs = await _mongoService.GetAllLogsAsync(id);

            var ph = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

            // Convert UTC times to local timezone
            foreach (var l in Logs)
            {
                l.Date = TimeZoneInfo.ConvertTimeFromUtc(l.Date, ph);

                if (l.TimeIn.HasValue)
                    l.TimeIn = TimeZoneInfo.ConvertTimeFromUtc(l.TimeIn.Value, ph);

                if (l.TimeOut.HasValue)
                    l.TimeOut = TimeZoneInfo.ConvertTimeFromUtc(l.TimeOut.Value, ph);
            }

            // Filter logs for the current month
            var now = DateTime.Now;
            var currentMonthLogs = Logs
                .Where(l => l.Date.Month == now.Month && l.Date.Year == now.Year)
                .ToList();

            // Summary calculations
            TotalWorkedDays = currentMonthLogs.Count(l => l.TimeIn.HasValue);
            int totalDaysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            AbsencesThisMonth = totalDaysInMonth - TotalWorkedDays;

            TotalHoursThisMonth = 0;
            OvertimeThisMonth = 0;
            UndertimeThisMonth = 0;

            foreach (var l in currentMonthLogs)
            {
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

            AttendanceRate = Math.Round((double)TotalWorkedDays / totalDaysInMonth * 100, 2);

            // Chart JSON for punctuality (Time In)
            PunctualityJson = JsonSerializer.Serialize(new
            {
                labels = currentMonthLogs.Select(l => l.Date.ToString("MM/dd")),
                datasets = new[]
                {
            new
            {
                label = "Time In",
                data = currentMonthLogs.Select(l => l.TimeIn?.Hour ?? 0)
            }
        }
            });

            // Chart JSON for work hours
            WorkHoursJson = JsonSerializer.Serialize(new
            {
                labels = currentMonthLogs.Select(l => l.Date.ToString("MM/dd")).ToList(),
                datasets = new[]
                {
            new
            {
                label = "Total Hours",
                data = currentMonthLogs.Select(l =>
                {
                    if (string.IsNullOrWhiteSpace(l.TotalHours))
                        return 0d;

                    var parts = l.TotalHours.Split(' ');
                    int hours = 0, minutes = 0;

                    foreach (var p in parts)
                    {
                        if (p.EndsWith("h"))
                            int.TryParse(p.Replace("h", ""), out hours);
                        else if (p.EndsWith("m"))
                            int.TryParse(p.Replace("m", ""), out minutes);
                    }

                    return hours + (minutes / 60.0);
                }).ToList()
            }
        }
            });
        }

        // Export monthly PDF
        public async Task<IActionResult> OnPostExportPDFAsync()
            {
                var id = HttpContext.Session.GetString("UserEmployeeId");
                if (id == null) return RedirectToPage("/Index");

                var employee = await _mongoService.GetByEmployeeIdAsync(id);
                if (employee == null) return RedirectToPage("/Index");

                var logs = await _mongoService.GetAllLogsAsync(id);

                var now = DateTime.Now;
                byte[] pdf = await _pdfService.GenerateMonthlyReportAsync(employee, logs, now.Month, now.Year);

                return File(pdf, "application/pdf", $"MonthlyReport_{employee.EmployeeId}_{now:yyyyMM}.pdf");
            }

            // Export single day PDF. Accepts date string in yyyy-MM-dd format.
            public async Task<IActionResult> OnPostExportDailyAsync(string date)
            {
                var id = HttpContext.Session.GetString("UserEmployeeId");
                if (id == null) return RedirectToPage("/Index");

                var employee = await _mongoService.GetByEmployeeIdAsync(id);
                if (employee == null) return RedirectToPage("/Index");

                if (!DateTime.TryParse(date, out var dt)) return BadRequest("Invalid date");

                // Find record for that date
                var logs = await _mongoService.GetAllLogsAsync(id);
                var record = logs.FirstOrDefault(l => l.Date.Date == dt.Date);
                if (record == null) return NotFound();

                var pdf = await _pdfService.GenerateDailyReportAsync(employee, record);
                return File(pdf, "application/pdf", $"DailyReport_{employee.EmployeeId}_{dt:yyyyMMdd}.pdf");
            }
        }
    }
