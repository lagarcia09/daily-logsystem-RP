using System;

namespace DailyLogSystem.Models
{
    public class LoginToken
    {
        public string Token { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string Role { get; set; } = default!; // "Admin" or "User"
        public DateTime Expiry { get; set; }
        public bool IsUsed { get; set; }
    }
}
