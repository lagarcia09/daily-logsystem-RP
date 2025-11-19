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

        // === EMPLOYEE FUNCTIONS ===
        public async Task<List<Employee>> GetAllEmployeesAsync() =>
            await _employees.Find(_ => true).ToListAsync();

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
            var update = Builders<Employee>.Update.Set("IsActive", false); // only works if you add IsActive later
            await _employees.UpdateOneAsync(filter, update);
        }

        public async Task ResetEmployeePasswordAsync(string employeeId)
        {
            // Stub: implement if you add password fields later
            await Task.CompletedTask;
        }

        // === ADMIN FUNCTIONS ===
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

        // === ATTENDANCE FUNCTIONS ===
        public async Task<List<TodayRecord>> GetAllRecordsAsync() =>
            await _attendance.Find(_ => true).ToListAsync();

        public async Task<List<TodayRecord>> GetAllRecordsByEmployeeAsync(string employeeId)
        {
            var filter = Builders<TodayRecord>.Filter.Eq(r => r.EmployeeId, employeeId);
            return await _attendance.Find(filter).SortBy(r => r.Date).ToListAsync();
        }

        public async Task<List<TodayRecord>> GetAllLogsAsync(string employeeId)
        {
            var filter = Builders<TodayRecord>.Filter.Eq(x => x.EmployeeId, employeeId);
            return await _attendance.Find(filter).SortByDescending(x => x.Date).ToListAsync();
        }

        public async Task<TodayRecord?> GetTodayRecordAsync(string employeeId)
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            var nowPh = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var startOfDay = nowPh.Date;
            var endOfDay = startOfDay.AddDays(1);

            return await _attendance.Find(r =>
                r.EmployeeId == employeeId &&
                r.Date >= startOfDay &&
                r.Date < endOfDay
            ).FirstOrDefaultAsync();
        }

        public async Task RecordTimeInAsync(string employeeId, DateTime phTime)
        {
            var day = phTime.Date;
            var record = await _attendance.Find(r => r.EmployeeId == employeeId && r.Date == day).FirstOrDefaultAsync();

            if (record == null)
            {
                var newRecord = new TodayRecord
                {
                    EmployeeId = employeeId,
                    Date = day,
                    TimeIn = phTime
                };
                await _attendance.InsertOneAsync(newRecord);
            }
        }

        public async Task RecordTimeOutAsync(string employeeId, DateTime phTime)
        {
            var day = phTime.Date;
            var record = await _attendance.Find(r => r.EmployeeId == employeeId && r.Date == day).FirstOrDefaultAsync();

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

        // === Reports (stub without EPPlus) ===
        public async Task<byte[]> GenerateExcelReportAsync(List<TodayRecord> logs)
        {
            // Stub: replace with EPPlus or ClosedXML if you want Excel export
            return await Task.FromResult(Array.Empty<byte>());
        }
    }
}
