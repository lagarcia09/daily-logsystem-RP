using Microsoft.AspNetCore.Mvc.RazorPages;
using DailyLogSystem.Services;
using DailyLogSystem.Models;

namespace DailyLogSystem.Pages
{
    public class CalendarModel : PageModel
    {
        private readonly MongoDbService _mongoService;

        public List<TodayRecord> WorkLogs { get; set; } = new List<TodayRecord>();



        public CalendarModel(MongoDbService mongoService)
        {
            _mongoService = mongoService;
        }

        public async Task OnGet()
        {
            var userId = HttpContext.Session.GetString("UserEmployeeId");

            if (string.IsNullOrEmpty(userId))
            {
                // User not logged in → redirect to login
                Response.Redirect("/Index");
                return;
            }

            WorkLogs = await _mongoService.GetAllRecordsByEmployeeAsync(userId);

        }

    }
}
