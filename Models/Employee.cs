using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DailyLogSystem.Models
{
    [BsonIgnoreExtraElements]
    public class Employee
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }   // MongoDB auto-generated

        // Personal Information
        public string FullName { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;

        // Employment Details
        public string Department { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string WorkSchedule { get; set; } = string.Empty;

        // Login Credential
        public string Password { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;


    }
}
