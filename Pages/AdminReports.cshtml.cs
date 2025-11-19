using DailyLogSystem.Models;
using DailyLogSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DailyLogSystem.Pages
{
    public class AdminReportsModel : PageModel
    {
        private readonly MongoDbService _mongoService;
        private readonly IPdfService _pdfService;

        public List<TodayRecord> AttendanceLogs { get; set; } = new();

        public AdminReportsModel(MongoDbService mongoService, IPdfService pdfService)
        {
            _mongoService = mongoService;
            _pdfService = pdfService;
        }

        public async Task OnGet()
        {
            AttendanceLogs = await _mongoService.GetAllRecordsAsync();
        }

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

            return File(excel,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"AdminReport_{DateTime.Today:yyyyMM}.xlsx");
        }
    }
}
