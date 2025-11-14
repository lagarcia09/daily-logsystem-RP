using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

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
    }
}
