using DailyLogSystem.Models;
using DailyLogSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace DailyLogSystem.Pages
{
    public class AdminReportsModel : PageModel
    {
        private readonly MongoDbService _mongoService;
        private readonly IPdfService _pdfService;

        public List<TodayRecord> AttendanceLogs { get; set; } = new();
        public List<Employee> Employees { get; set; } = new();

        // filters
        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? Department { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? Role { get; set; }
        [BindProperty(SupportsGet = true)]
        public DateTime? DateFrom { get; set; }
        [BindProperty(SupportsGet = true)]
        public DateTime? DateTo { get; set; }

        // metrics
        public int TotalEmployees { get; set; }
        public double TotalWorkedHours { get; set; }
        public double OvertimeHours { get; set; }
        public int AbsencesCount { get; set; }
        public double AttendanceRate { get; set; }

        public string TotalWorkedHoursString => $"{TotalWorkedHours:0.##} hrs";
        public string OvertimeHoursString => $"{OvertimeHours:0.##} hrs";
        public string AttendanceRateString => $"{AttendanceRate:0.##}%";

        public AdminReportsModel(MongoDbService mongoService, IPdfService pdfService)
        {
            _mongoService = mongoService ?? throw new ArgumentNullException(nameof(mongoService));
            _pdf_service_check(pdfService);
            _pdfService = pdfService;
        }

        private void _pdf_service_check(IPdfService pdfService)
        {
            if (pdfService == null) throw new ArgumentNullException(nameof(pdfService));
        }

        public async Task<IActionResult> OnGet()
        {
            var adminId = HttpContext.Session.GetString("AdminId");
            if (string.IsNullOrEmpty(adminId))
            {
                return RedirectToPage("/Index");
            }

            Employees = await _mongoService.GetAllEmployeesAsync();
            TotalEmployees = Employees?.Count ?? 0;

            var logs = await _mongo_service_get_records_safe();

            // Map employee names
            foreach (var rec in logs)
            {
                var emp = Employees!.FirstOrDefault(e => e.EmployeeId == rec.EmployeeId);
                if (emp != null)
                {
                    rec.FullName = emp.FullName;
                }
            }

            // Apply filters
            if (DateFrom.HasValue)
                logs = logs.Where(l => l.Date.Date >= DateFrom.Value.Date).ToList();

            if (DateTo.HasValue)
                logs = logs.Where(l => l.Date.Date <= DateTo.Value.Date).ToList();

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var s = Search.Trim().ToLower();
                logs = logs.Where(l =>
                    (l.EmployeeId ?? "").ToLower().Contains(s) ||
                    (l.FullName ?? "").ToLower().Contains(s)
                ).ToList();
            }

            if (!string.IsNullOrWhiteSpace(Department) || !string.IsNullOrWhiteSpace(Role))
            {
                logs = logs.Where(l =>
                {
                    var emp = Employees!.FirstOrDefault(e => e.EmployeeId == l.EmployeeId);
                    if (emp == null) return false;

                    if (!string.IsNullOrWhiteSpace(Department) &&
                        !(emp.Department?.Contains(Department, StringComparison.OrdinalIgnoreCase) ?? false))
                        return false;

                    if (!string.IsNullOrWhiteSpace(Role) &&
                        !(emp.Position?.Contains(Role, StringComparison.OrdinalIgnoreCase) ?? false))
                        return false;

                    return true;
                }).ToList();
            }

            AttendanceLogs = logs.OrderByDescending(x => x.Date).ToList();

            ComputeMetricsAndCharts(AttendanceLogs);

            return Page();   // ALWAYS return Page() since it's IActionResult
        }

        private async Task<List<TodayRecord>> _mongo_service_get_records_safe()
        {
            var result = await _mongoService.GetAllRecordsAsync();
            return result ?? new List<TodayRecord>();
        }

        private void ComputeMetricsAndCharts(List<TodayRecord> logs)
        {
            AbsencesCount = logs.Count(l => string.Equals(l.Status, "ABSENT", StringComparison.OrdinalIgnoreCase));
            var presentCount = logs.Count(l => !string.Equals(l.Status, "ABSENT", StringComparison.OrdinalIgnoreCase));
            AttendanceRate = TotalEmployees == 0 ? 0 : (presentCount / (double)TotalEmployees) * 100.0;

            double total = 0;
            double overtime = 0;

            foreach (var r in logs)
            {
                total += ParseHours(r.TotalHours);
                overtime += ParseHours(r.OvertimeHours);
            }

            TotalWorkedHours = total;
            OvertimeHours = overtime;
        }

        private double ParseHours(string? hoursString)
        {
            if (string.IsNullOrWhiteSpace(hoursString)) return 0;
            hoursString = hoursString.Trim();

            if (hoursString.Contains('h'))
            {
                try
                {
                    var hPart = 0;
                    var mPart = 0;
                    var parts = hoursString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var p in parts)
                    {
                        if (p.EndsWith("h") && int.TryParse(p.TrimEnd('h'), out var h)) hPart = h;
                        if (p.EndsWith("m") && int.TryParse(p.TrimEnd('m'), out var m)) mPart = m;
                    }
                    return hPart + (mPart / 60.0);
                }
                catch { return 0; }
            }

            if (double.TryParse(hoursString, out var d)) return d;
            return 0;
        }

        // Exports
        public async Task<IActionResult> OnPostExportMonthlyAsync()
        {
            var logs = await _mongoService.GetAllRecordsAsync();
            var pdf = await _pdfService.GenerateMonthlyReportAsync(logs, DateTime.Today.Month, DateTime.Today.Year);
            return File(pdf, "application/pdf", $"AdminMonthlyReport_{DateTime.Today:yyyyMM}.pdf");
        }

        public async Task<IActionResult> OnPostExportExcelAsync()
        {
            var logs = await _mongoService.GetAllRecordsAsync();
            var excel = await _mongoService.GenerateExcelReportAsync(logs);
            return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"AdminReport_{DateTime.Today:yyyyMM}.xlsx");
        }

        public async Task<IActionResult> OnPostSaveRecordAsync([FromForm] TodayRecord model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Id))
                return RedirectToPage();

            if (model.TimeIn.HasValue && model.TimeOut.HasValue)
            {
                var span = model.TimeOut.Value - model.TimeIn.Value;
                model.TotalHours = $"{(int)span.TotalHours}h {span.Minutes}m";
            }

            await _mongoService.UpdateRecordAsync(model);
            TempData["Success"] = "Record updated.";
            return RedirectToPage();
        }
    }
}
