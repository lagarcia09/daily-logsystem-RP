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

            if (TodayRecord == null)
                return; 
            var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

            TodayRecord.Date = TimeZoneInfo.ConvertTimeFromUtc(TodayRecord.Date, phTimeZone);

            if (TodayRecord.TimeIn.HasValue)
                TodayRecord.TimeIn = TimeZoneInfo.ConvertTimeFromUtc(TodayRecord.TimeIn.Value, phTimeZone);

            if (TodayRecord.TimeOut.HasValue)
                TodayRecord.TimeOut = TimeZoneInfo.ConvertTimeFromUtc(TodayRecord.TimeOut.Value, phTimeZone);

            
          

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

               
                var lateDuration = timeIn - officeStart;

                if (lateDuration.TotalHours >= 2)
                {
                    TodayRecord.Status = "ABSENT";
                    TodayRecord.TotalHours = "0";
                    TodayRecord.OvertimeHours = "0";
                }
                else
                {
                    
                    if (timeIn > officeStart)
                        TodayRecord.Status = "LATE";
                    else
                        TodayRecord.Status = "ON TIME";

                   
                    if (TodayRecord.TimeOut.HasValue)
                    {
                        var timeOut = TodayRecord.TimeOut.Value;

                        
                        var total = timeOut - timeIn;
                        TodayRecord.TotalHours = total.TotalHours.ToString("0.00");

                        
                        if (timeOut > officeEnd)
                        {
                            var overtime = timeOut - officeEnd;
                            TodayRecord.OvertimeHours = overtime.TotalHours.ToString("0.00");
                            TodayRecord.UndertimeHours = "0";
                            TodayRecord.Status = "OVERTIME";
                        }
                        
                        else if (timeOut < officeEnd)
                        {
                            var undertime = officeEnd - timeOut;
                            TodayRecord.UndertimeHours = undertime.TotalHours.ToString("0.00");
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

            
            await _mongoService.UpdateTodayRecordStatusAsync(emp.EmployeeId, TodayRecord);

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
