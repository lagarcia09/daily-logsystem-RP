using DailyLogSystem.Models;
using DailyLogSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DailyLogSystem.Pages
{
    public class AdminAttendanceModel : PageModel
    {
        private readonly MongoDbService _mongoService;

        public List<TodayRecord> AttendanceLogs { get; set; } = new();

        // date filter (from the UI)
        [BindProperty(SupportsGet = true)]
        public DateTime? SelectedDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        public AdminAttendanceModel(MongoDbService mongoService)
        {
            _mongoService = mongoService;
        }

        public async Task OnGetAsync()
        {
            AttendanceLogs = await _mongoService.GetAllRecordsAsync();

            // Ensure DisplayName fallback
            AttendanceLogs.ForEach(l =>
            {
                if (string.IsNullOrEmpty(l.FullName) && !string.IsNullOrEmpty(l.EmployeeName))
                    l.FullName = l.EmployeeName;
            });

            if (SelectedDate.HasValue)
            {
                DateTime target = SelectedDate.Value.Date;
                AttendanceLogs = AttendanceLogs.Where(l => l.Date.Date == target).ToList();
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter))
            {
                AttendanceLogs = AttendanceLogs.Where(l => (l.Status ?? "") == StatusFilter).ToList();
            }
        }

        // Edit attendance handler
        public async Task<IActionResult> OnPostEditAttendanceAsync(string id, string date, string timeIn, string timeOut, string status, string overtime)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["SuccessMessage"] = "Invalid record ID.";
                return RedirectToPage();
            }

            // Get the record
            var record = await _mongoService.GetRecordByIdAsync(id);
            if (record == null)
            {
                TempData["SuccessMessage"] = "Record not found.";
                return RedirectToPage();
            }

            // Parse provided date (the date field is readonly in UI but included to align day)
            DateTime parsedDate;
            if (!DateTime.TryParse(date, out parsedDate))
            {
                parsedDate = record.Date.Date;
            }

            // Parse timeIn/timeOut inputs (datetime-local format)
            DateTime? parsedTimeIn = null;
            DateTime? parsedTimeOut = null;

            if (!string.IsNullOrWhiteSpace(timeIn))
            {
                if (DateTime.TryParse(timeIn, out var tIn))
                    parsedTimeIn = tIn;
            }

            if (!string.IsNullOrWhiteSpace(timeOut))
            {
                if (DateTime.TryParse(timeOut, out var tOut))
                    parsedTimeOut = tOut;
            }

            // Apply updates to the record
            record.Date = parsedDate.Date;
            record.TimeIn = parsedTimeIn;
            record.TimeOut = parsedTimeOut;

            // Recalculate total hours, overtime, undertime and status (simple business rules)
            var officeStart = record.Date.Date.AddHours(8);   // 08:00
            var officeEnd = record.Date.Date.AddHours(17);    // 17:00

            // default values
            record.TotalHours = "0";
            record.OvertimeHours = "0";
            record.UndertimeHours = "";

            // determine status and hours
            if (!record.TimeIn.HasValue)
            {
                record.Status = "ABSENT";
                record.TotalHours = "0";
                record.OvertimeHours = "0";
            }
            else
            {
                var timeInVal = record.TimeIn.Value;

                var lateDuration = timeInVal - officeStart;
                if (lateDuration.TotalHours >= 2)
                {
                    record.Status = "ABSENT";
                    record.TotalHours = "0";
                    record.OvertimeHours = "0";
                }
                else
                {
                    if (timeInVal > officeStart)
                        record.Status = "LATE";
                    else
                        record.Status = "ON TIME";

                    if (record.TimeOut.HasValue)
                    {
                        var timeOutVal = record.TimeOut.Value;
                        var total = timeOutVal - timeInVal;
                        record.TotalHours = $"{(int)total.TotalHours}h {total.Minutes}m";

                        if (timeOutVal > officeEnd)
                        {
                            var overtimeSpan = timeOutVal - officeEnd;
                            record.OvertimeHours = overtimeSpan.TotalHours.ToString("0.00");
                            record.UndertimeHours = "0";
                            record.Status = "OVERTIME";
                        }
                        else if (timeOutVal < officeEnd)
                        {
                            var undertime = officeEnd - timeOutVal;
                            record.UndertimeHours = undertime.TotalHours.ToString("0.00");
                            record.OvertimeHours = "0";
                            record.Status = "UNDERTIME";
                        }
                        else
                        {
                            record.OvertimeHours = "0";
                            record.UndertimeHours = "0";
                        }
                    }
                    else
                    {
                        record.TotalHours = "0";
                        record.OvertimeHours = "0";
                        record.UndertimeHours = "0";
                    }
                }
            }

            // If admin manually provided overtime value, override
            if (!string.IsNullOrWhiteSpace(overtime))
            {
                // store as string (admin provided)
                record.OvertimeHours = overtime;
            }

            // If admin explicitly selected a status in the modal, apply it
            if (!string.IsNullOrWhiteSpace(status))
            {
                record.Status = status;
            }

            // Persist changes
            await _mongoService.UpdateRecordAsync(record);

            TempData["SuccessMessage"] = "Attendance updated successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostOverrideLogAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
                return Page();

            await _mongoService.OverrideAttendanceLogAsync(id);
            return RedirectToPage();
        }
    }
}
