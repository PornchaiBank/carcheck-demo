using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Carcheck.Api;

public sealed class LoginSessionStore
{
    public static readonly TimeSpan Lifetime=TimeSpan.FromHours(8);
    private readonly ConcurrentDictionary<string,SessionEntry> sessions=new();

    public (string Token,DateTimeOffset Expires) Create(LoginUser user)
    {
        var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expires=DateTimeOffset.UtcNow.Add(Lifetime);
        sessions[token]=new SessionEntry(user,expires);
        RemoveExpired();
        return(token,expires);
    }

    public bool TryGet(string? token,out LoginUser? user)
    {
        user=null;
        if(string.IsNullOrWhiteSpace(token)||!sessions.TryGetValue(token,out var entry))return false;
        if(entry.Expires<=DateTimeOffset.UtcNow){sessions.TryRemove(token,out _);return false;}
        user=entry.User;
        return true;
    }

    public void Revoke(string? token){if(!string.IsNullOrWhiteSpace(token))sessions.TryRemove(token,out _);}
    private void RemoveExpired(){var now=DateTimeOffset.UtcNow;foreach(var item in sessions)if(item.Value.Expires<=now)sessions.TryRemove(item.Key,out _);}
    private sealed record SessionEntry(LoginUser User,DateTimeOffset Expires);
}
