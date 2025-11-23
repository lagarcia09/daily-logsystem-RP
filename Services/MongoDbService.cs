using Microsoft.Extensions.Options;
using MongoDB.Driver;
using DailyLogSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DailyLogSystem.Services
{
    public class MongoDbSettings
    {
        public string ConnectionString { get; set; } = "";
        public string DatabaseName { get; set; } = "";
        public string EmployeeCollectionName { get; set; } = "Employees";
        public string AttendanceCollectionName { get; set; } = "AttendanceLogs";
        public string AdminCollectionName { get; set; } = "Admins";
    }

    public class MongoDbService
    {
        private readonly IMongoCollection<Employee> _employees;
        private readonly IMongoCollection<TodayRecord> _attendance;
        private readonly IMongoCollection<Admin> _admins;

        public MongoDbService(IOptions<MongoDbSettings> options)
        {
            var settings = options.Value;
            var client = new MongoClient(settings.ConnectionString);
            var database = client.GetDatabase(settings.DatabaseName);

            _employees = database.GetCollection<Employee>(settings.EmployeeCollectionName);
            _attendance = database.GetCollection<TodayRecord>(settings.AttendanceCollectionName);
            _admins = database.GetCollection<Admin>(settings.AdminCollectionName);
        }

        // ============================================================
        // EMPLOYEE METHODS
        // ============================================================
        public async Task<List<Employee>> GetAllEmployeesAsync()
        {
            return await _employees.Find(_ => true).ToListAsync();
        }

        public async Task AddEmployeeAsync(Employee emp)
        {
            if (string.IsNullOrWhiteSpace(emp.EmployeeId))
                emp.EmployeeId = Guid.NewGuid().ToString("N");

            await _employees.InsertOneAsync(emp);
        }

        public async Task<Employee?> GetByEmailAsync(string email) =>
            await _employees.Find(e => e.Email == email).FirstOrDefaultAsync();

        public async Task<Employee?> GetByEmployeeIdAsync(string id) =>
            await _employees.Find(e => e.EmployeeId == id).FirstOrDefaultAsync();

        public async Task DeactivateEmployeeAsync(string employeeId)
        {
            var filter = Builders<Employee>.Filter.Eq(e => e.EmployeeId, employeeId);
            var update = Builders<Employee>.Update.Set(e => e.IsActive, false);
            await _employees.UpdateOneAsync(filter, update);
        }

        public async Task ReactivateEmployeeAsync(string employeeId)
        {
            var filter = Builders<Employee>.Filter.Eq(e => e.EmployeeId, employeeId);
            var update = Builders<Employee>.Update.Set(e => e.IsActive, true);
            await _employees.UpdateOneAsync(filter, update);
        }

        public async Task<Employee?> ResetEmployeePasswordAsync(string employeeId, string newPassword)
        {
            var emp = await GetByEmployeeIdAsync(employeeId);
            if (emp == null) return null;

            emp.Password = newPassword;

            var filter = Builders<Employee>.Filter.Eq(e => e.EmployeeId, employeeId);
            await _employees.ReplaceOneAsync(filter, emp);

            return emp;
        }

        // ============================================================
        // ADMIN METHODS
        // ============================================================
        public async Task CreateAdminAsync(Admin admin)
        {
            if (string.IsNullOrWhiteSpace(admin.AdminId))
                admin.AdminId = Guid.NewGuid().ToString("N");

            await _admins.InsertOneAsync(admin);
        }

        public async Task<Admin?> GetAdminByEmailAsync(string email) =>
            await _admins.Find(a => a.Email == email).FirstOrDefaultAsync();

        public async Task<Admin?> GetAdminByIdAsync(string adminId) =>
            await _admins.Find(a => a.AdminId == adminId).FirstOrDefaultAsync();

        // ============================================================
        // ATTENDANCE METHODS
        // ============================================================
        public async Task<List<TodayRecord>> GetAllRecordsAsync()
        {
            var logs = await _attendance.Find(_ => true).ToListAsync();
            var employees = await GetAllEmployeesAsync();

            foreach (var log in logs)
            {
                var emp = employees.FirstOrDefault(e => e.EmployeeId == log.EmployeeId);

                if (emp != null)
                {
                    // Always sync employee name
                    log.FullName = emp.FullName;
                    log.EmployeeName = emp.FullName;
                }
                else
                {
                    log.FullName = "Unknown";
                }
            }

            return logs;
        }


        public async Task<List<TodayRecord>> GetAllRecordsByEmployeeAsync(string employeeId)
        {
            var filter = Builders<TodayRecord>.Filter.Eq(r => r.EmployeeId, employeeId);
            return await _attendance.Find(filter).SortBy(r => r.Date).ToListAsync();
        }

        public async Task<List<TodayRecord>> GetAllLogsAsync(string employeeId)
        {
            var filter = Builders<TodayRecord>.Filter.Eq(r => r.EmployeeId, employeeId);
            return await _attendance.Find(filter)
                .SortByDescending(r => r.Date)
                .ToListAsync();
        }

        public async Task<TodayRecord?> GetTodayRecordAsync(string employeeId)
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            var nowPh = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

            var dayStart = nowPh.Date;
            var dayEnd = dayStart.AddDays(1);

            return await _attendance.Find(r =>
                r.EmployeeId == employeeId &&
                r.Date >= dayStart &&
                r.Date < dayEnd
            ).FirstOrDefaultAsync();
        }

        public async Task RecordTimeInAsync(string employeeId, DateTime phTime)
        {
            var record = await _attendance.Find(r =>
                r.EmployeeId == employeeId &&
                r.Date == phTime.Date
            ).FirstOrDefaultAsync();

            if (record == null)
            {
                var newRecord = new TodayRecord
                {
                    EmployeeId = employeeId,
                    Date = phTime.Date,
                    TimeIn = phTime
                };

                await _attendance.InsertOneAsync(newRecord);
            }
        }

        public async Task RecordTimeOutAsync(string employeeId, DateTime phTime)
        {
            var record = await _attendance.Find(r =>
                r.EmployeeId == employeeId &&
                r.Date == phTime.Date
            ).FirstOrDefaultAsync();

            if (record != null && !record.TimeOut.HasValue)
            {
                record.TimeOut = phTime;

                if (record.TimeIn.HasValue)
                {
                    var total = phTime - record.TimeIn.Value;
                    record.TotalHours = $"{(int)total.TotalHours}h {total.Minutes}m";
                }

                await _attendance.ReplaceOneAsync(r => r.Id == record.Id, record);
            }
        }

        public async Task UpdateTodayRecordStatusAsync(string employeeId, TodayRecord updatedRecord)
        {
            var filter = Builders<TodayRecord>.Filter.And(
                Builders<TodayRecord>.Filter.Eq(r => r.EmployeeId, employeeId),
                Builders<TodayRecord>.Filter.Eq(r => r.Date, updatedRecord.Date)
            );

            var update = Builders<TodayRecord>.Update
                .Set(r => r.Status, updatedRecord.Status)
                .Set(r => r.TotalHours, updatedRecord.TotalHours)
                .Set(r => r.OvertimeHours, updatedRecord.OvertimeHours)
                .Set(r => r.UndertimeHours, updatedRecord.UndertimeHours)
                .Set(r => r.TimeIn, updatedRecord.TimeIn)
                .Set(r => r.TimeOut, updatedRecord.TimeOut);

            await _attendance.UpdateOneAsync(filter, update);
        }

        public async Task OverrideAttendanceLogAsync(string logId)
        {
            var filter = Builders<TodayRecord>.Filter.Eq(r => r.Id, logId);
            var update = Builders<TodayRecord>.Update.Set(r => r.Status, "Corrected");
            await _attendance.UpdateOneAsync(filter, update);
        }

        public async Task<TodayRecord?> GetRecordByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            return await _attendance.Find(r => r.Id == id).FirstOrDefaultAsync();
        }

        public async Task UpdateRecordAsync(TodayRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.Id)) return;

            await _attendance.ReplaceOneAsync(r => r.Id == record.Id, record);
        }

        public async Task<byte[]> GenerateExcelReportAsync(List<TodayRecord> logs)
        {
            using var package = new OfficeOpenXml.ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Attendance Report");

            // Header row
            worksheet.Cells[1, 1].Value = "Employee Name";
            worksheet.Cells[1, 2].Value = "Employee ID";
            worksheet.Cells[1, 3].Value = "Date";
            worksheet.Cells[1, 4].Value = "Status";
            worksheet.Cells[1, 5].Value = "Time In";
            worksheet.Cells[1, 6].Value = "Time Out";
            worksheet.Cells[1, 7].Value = "Total Hours";

            int row = 2;

            // Get all employees to match names
            var employees = await GetAllEmployeesAsync();

            foreach (var log in logs)
            {
                var employee = employees.FirstOrDefault(e => e.EmployeeId == log.EmployeeId);

                worksheet.Cells[row, 1].Value = employee?.FullName ?? "Unknown";
                worksheet.Cells[row, 2].Value = log.EmployeeId;
                worksheet.Cells[row, 3].Value = log.Date.ToString("yyyy-MM-dd");
                worksheet.Cells[row, 4].Value = log.Status ?? "";
                worksheet.Cells[row, 5].Value = log.TimeIn?.ToString("hh:mm tt") ?? "";
                worksheet.Cells[row, 6].Value = log.TimeOut?.ToString("hh:mm tt") ?? "";
                worksheet.Cells[row, 7].Value = log.TotalHours ?? "";

                row++;
            }

            worksheet.Cells.AutoFitColumns();

            return package.GetAsByteArray();
        }

    }
}
