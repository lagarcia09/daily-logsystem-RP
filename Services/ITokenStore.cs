using DailyLogSystem.Models;

namespace DailyLogSystem.Services
{
    public interface ITokenStore
    {
        void Add(LoginToken token);
        LoginToken? Get(string token);
        void MarkUsed(string token);
    }
}
