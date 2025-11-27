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
        private readonly IMongoDatabase _database;


        // ⭐ FIXED: this is the ONLY constructor
        public MongoDbService(IOptions<MongoDbSettings> options)
        {
            var settings = options.Value;
            var client = new MongoClient(settings.ConnectionString);

            _database = client.GetDatabase(settings.DatabaseName); // <-- initialize _database first

            _employees = _database.GetCollection<Employee>(settings.EmployeeCollectionName);
            _attendance = _database.GetCollection<TodayRecord>(settings.AttendanceCollectionName);
            _admins = _database.GetCollection<Admin>(settings.AdminCollectionName);
        }



        // ============================================================
        // EMPLOYEE METHODS
        // ============================================================
        public async Task<List<Employee>> GetAllEmployeesAsync()
        {
            return await _employees.Find(_ => true).ToListAsync();
        }

        public async Task AddEmployeeAsync(Employee employee)
        {
            await _employees.InsertOneAsync(employee);
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

                log.FullName = emp?.FullName ?? "Unknown";
               

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


        public async Task<byte[]> GenerateExcelReportAsync(List<TodayRecord> logs)
        {
            using var package = new OfficeOpenXml.ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Attendance Report");

            // Headers
            ws.Cells[1, 1].Value = "Employee ID";
            ws.Cells[1, 2].Value = "Name";
            ws.Cells[1, 3].Value = "Date";
            ws.Cells[1, 4].Value = "Time In";
            ws.Cells[1, 5].Value = "Time Out";
            ws.Cells[1, 6].Value = "Status";
            ws.Cells[1, 7].Value = "Total Hours";
            ws.Cells[1, 8].Value = "Overtime";
            ws.Cells[1, 9].Value = "Has Photo";

            int row = 2;

            foreach (var log in logs)
            {
                ws.Cells[row, 1].Value = log.EmployeeId;
                ws.Cells[row, 2].Value = log.FullName;
                ws.Cells[row, 3].Value = log.Date.ToString("yyyy-MM-dd");

                ws.Cells[row, 4].Value = log.TimeIn?.ToString("hh:mm tt") ?? "";
                ws.Cells[row, 5].Value = log.TimeOut?.ToString("hh:mm tt") ?? "";

                ws.Cells[row, 6].Value = log.Status;
                ws.Cells[row, 7].Value = log.TotalHours;
                ws.Cells[row, 8].Value = log.OvertimeHours;


                row++;
            }

            ws.Cells.AutoFitColumns();

            return await Task.FromResult(package.GetAsByteArray());
        }


        public async Task UpdateRecordAsync(TodayRecord updated)
        {
            var filter = Builders<TodayRecord>.Filter.Eq(r => r.Id, updated.Id);

            var update = Builders<TodayRecord>.Update
                .Set(r => r.TimeIn, updated.TimeIn)
                .Set(r => r.TimeOut, updated.TimeOut)
                .Set(r => r.TotalHours, updated.TotalHours)
                .Set(r => r.Status, updated.Status)
                .Set(r => r.OvertimeHours, updated.OvertimeHours);

            await _attendance.UpdateOneAsync(filter, update);
        }

        public async Task UpdateTodayRecordStatusAsync(string employeeId, TodayRecord updatedRecord)
        {
            var filter = Builders<TodayRecord>.Filter.And(
                Builders<TodayRecord>.Filter.Eq(r => r.EmployeeId, employeeId),
                Builders<TodayRecord>.Filter.Eq(r => r.Date, updatedRecord.Date.Date)
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

        public async Task<TodayRecord?> GetRecordByIdAsync(string id)
        {
            var filter = Builders<TodayRecord>.Filter.Eq(r => r.Id, id);
            return await _attendance.Find(filter).FirstOrDefaultAsync();
        }

        public async Task OverrideAttendanceLogAsync(string id)
        {
            var filter = Builders<TodayRecord>.Filter.Eq(r => r.Id, id);

            var update = Builders<TodayRecord>.Update
                .Set(r => r.TimeIn, null)
                .Set(r => r.TimeOut, null)
                .Set(r => r.TotalHours, "0")
                .Set(r => r.OvertimeHours, "0")
                .Set(r => r.UndertimeHours, "0")
                .Set(r => r.Status, "RESET BY ADMIN");

            await _attendance.UpdateOneAsync(filter, update);
        }

        public async Task UpdateProfileAsync(string employeeId, string fullName, string contact, string address)
        {
            var update = Builders<Employee>.Update
                .Set(x => x.FullName, fullName)
                .Set(x => x.ContactNumber, contact)
                .Set(x => x.Address, address);

            await _employees.UpdateOneAsync(x => x.EmployeeId == employeeId, update);
        }

        public async Task<Employee?> GetUserByEmailAsync(string email)
        {
            return await _employees
                .Find(e => e.Email == email)
                .FirstOrDefaultAsync();
        }

        public async Task UpdateUserAsync(Employee updatedUser)
        {
            await _employees.ReplaceOneAsync(
                e => e.Id == updatedUser.Id,
                updatedUser
            );
        }

    }


}

