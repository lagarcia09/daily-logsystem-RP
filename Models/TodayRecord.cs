using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace DailyLogSystem.Models
{
    public class TodayRecord
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("EmployeeId")]
        public string EmployeeId { get; set; } = "";

        [BsonElement("Date")]
        public DateTime Date { get; set; }

        [BsonElement("TimeIn")]
        public DateTime? TimeIn { get; set; }

        [BsonElement("TimeOut")]
        public DateTime? TimeOut { get; set; }

        [BsonElement("TotalHours")]
        public string? TotalHours { get; set; }

        public string Status { get; set; } = "";
        public string OvertimeHours { get; set; } = "";
        public string UndertimeHours { get; set; } = "";

        // MongoDB OLD FIELD
        [BsonElement("EmployeeName")]
        public string? EmployeeName { get; set; }

        // MongoDB NEW FIELD
        [BsonElement("FullName")]
        public string? FullName { get; set; }

        // Avoid crash when unexpected fields exist
        [BsonExtraElements]
        [JsonIgnore] // <-- Ignore Extra when serializing to JSON
        public BsonDocument? Extra { get; set; }

        // Auto-computed unified FullName (use this in your code)
        [BsonIgnore]
        public string DisplayName => FullName ?? EmployeeName ?? "";
    }
}
