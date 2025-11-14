using Microsoft.Extensions.Options;
using MongoDB.Driver;
using DailyLogSystem.Models;
using System;
using System.Threading.Tasks;

namespace DailyLogSystem.Services
{
    public class MongoDbSettings
    {
        public string ConnectionString { get; set; } = "";
        public string DatabaseName { get; set; } = "";
        public string EmployeeCollectionName { get; set; } = "Employees";
        public string AttendanceCollectionName { get; set; } = "AttendanceLogs";
        public string AdminCollectionName { get; set; } = "Admins"; // ✅ Added for Admins
    }

    public class MongoDbService
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<Employee> _employees;
        private readonly IMongoCollection<TodayRecord> _records;



        public MongoDbService(IOptions<MongoDbSettings> options)
        {
            var settings = options.Value;
            var client = new MongoClient(settings.ConnectionString);
            _database = client.GetDatabase(settings.DatabaseName);

            _employees = _database.GetCollection<Employee>(settings.EmployeeCollectionName);
            _records = _database.GetCollection<TodayRecord>(settings.AttendanceCollectionName);
        }

        // === EMPLOYEE FUNCTIONS ===
        public async Task CreateEmployeeAsync(Employee emp) =>
            await _employees.InsertOneAsync(emp);

        public async Task<Employee?> GetByEmailAsync(string email) =>
            await _employees.Find(e => e.Email == email).FirstOrDefaultAsync();

        public async Task<Employee?> GetByEmployeeIdAsync(string id) =>
            await _employees.Find(e => e.EmployeeId == id).FirstOrDefaultAsync();

        // === ATTENDANCE FUNCTIONS ===
        public async Task<TodayRecord?> GetTodayRecordAsync(string employeeId)
        {
            var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            var phTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);
            var todayPhDate = phTime.Date;

            var startOfDay = todayPhDate;
            var endOfDay = todayPhDate.AddDays(1);

            return await _records.Find(r =>
                r.EmployeeId == employeeId &&
                r.Date >= startOfDay &&
                r.Date < endOfDay
            ).FirstOrDefaultAsync();
        }

        public async Task RecordTimeInAsync(string employeeId, DateTime phTime)
        {
            var todayPhDate = phTime.Date;

            var record = await _records.Find(r => r.EmployeeId == employeeId && r.Date == todayPhDate)
                                       .FirstOrDefaultAsync();

            if (record == null)
            {
                var newRecord = new TodayRecord
                {
                    EmployeeId = employeeId,
                    Date = todayPhDate,
                    TimeIn = phTime
                };
                await _records.InsertOneAsync(newRecord);
            }
        }

        public async Task RecordTimeOutAsync(string employeeId, DateTime phTime)
        {
            var startOfDay = phTime.Date;
            var endOfDay = startOfDay.AddDays(1);

            var record = await _records.Find(r =>
                r.EmployeeId == employeeId &&
                r.Date >= startOfDay &&
                r.Date < endOfDay
            ).FirstOrDefaultAsync();

            if (record != null && record.TimeOut == null)
            {
                record.TimeOut = phTime;

                if (record.TimeIn.HasValue)
                {
                    var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
                    var timeInPH = TimeZoneInfo.ConvertTime(record.TimeIn.Value, phTimeZone);
                    var timeOutPH = TimeZoneInfo.ConvertTime(record.TimeOut.Value, phTimeZone);
                    var total = timeOutPH - timeInPH;

                    record.TotalHours = $"{(int)total.TotalHours}h {total.Minutes}m";
                }

                await _records.ReplaceOneAsync(r => r.Id == record.Id, record);
            }
        }

        // === ADMIN FUNCTIONS ===
        public async Task CreateAdminAsync(Admin admin)
        {
            var admins = _database.GetCollection<Admin>("Admins");
            await admins.InsertOneAsync(admin);
        }

        public async Task<Admin?> GetAdminByEmailAsync(string email)
        {
            var admins = _database.GetCollection<Admin>("Admins");
            return await admins.Find(a => a.Email == email).FirstOrDefaultAsync();
        }

        public async Task<Admin?> GetAdminByIdAsync(string adminId)
        {
            var admins = _database.GetCollection<Admin>("Admins");
            return await admins.Find(a => a.AdminId == adminId).FirstOrDefaultAsync();
        }

        public async Task<List<TodayRecord>> GetAllRecordsByEmployeeAsync(string employeeId)
        {
            return await _records
                .Find(r => r.EmployeeId == employeeId)
                .SortBy(r => r.Date)
                .ToListAsync();
        }

        public async Task<List<TodayRecord>> GetAllLogsAsync(string employeeId)
        {
            var filter = Builders<TodayRecord>.Filter.Eq(x => x.EmployeeId, employeeId);

            return await _records
                .Find(filter)
                .SortByDescending(x => x.Date)
                .ToListAsync();
        }








    }
}
