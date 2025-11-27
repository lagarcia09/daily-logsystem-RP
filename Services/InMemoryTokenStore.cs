using System.Collections.Concurrent;
using DailyLogSystem.Models;

namespace DailyLogSystem.Services
{
    public class InMemoryTokenStore : ITokenStore
    {
        private readonly ConcurrentDictionary<string, LoginToken> _tokens = new();

        public void Add(LoginToken token) => _tokens[token.Token] = token;

        public LoginToken? Get(string token)
        {
            _tokens.TryGetValue(token, out var t);
            return t;
        }

        public void MarkUsed(string token)
        {
            if (_tokens.TryGetValue(token, out var t))
            {
                t.IsUsed = true;
                _tokens[token] = t;
            }
        }
    }
}
