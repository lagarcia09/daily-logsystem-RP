using DailyLogSystem.Models;
using DailyLogSystem.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DailyLogSystem.Pages
{
    public class AdminOverviewModel : PageModel
    {
        private readonly MongoDbService _mongoService;

        public int TotalEmployees { get; set; }
        public int AbsencesToday { get; set; }
        public double OvertimeThisWeek { get; set; }

        public List<Employee> Employees { get; set; } = new();
        public List<TodayRecord> AttendanceLogs { get; set; } = new();

        public AdminOverviewModel(MongoDbService mongoService)
        {
            _mongoService = mongoService;
        }

        public async Task OnGet()
        {
            Employees = await _mongoService.GetAllEmployeesAsync();
            TotalEmployees = Employees.Count;

            AttendanceLogs = await _mongoService.GetAllRecordsAsync();

            AbsencesToday = AttendanceLogs
                .Count(l => l.Date.Date == DateTime.Today && l.Status == "ABSENT");

            var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            OvertimeThisWeek = AttendanceLogs
                .Where(l => l.Date >= startOfWeek)
                .Sum(l => double.TryParse(l.OvertimeHours, out var ot) ? ot : 0);
        }
    }
}
