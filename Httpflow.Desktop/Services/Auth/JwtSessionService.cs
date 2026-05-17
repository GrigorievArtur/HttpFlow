using System;
using System.Text.Json;
using System.Threading.Tasks;
using GitCredentialManager;
using Httpflow.Desktop.Models.Auth;

namespace Httpflow.Desktop.Services.Auth;

public sealed class JwtSessionService
{
    private const string Service = "https://httpflow.local/auth";
    private const string Account = "jwt";

    private readonly ICredentialStore _store;

    public JwtSessionService()
    {
        _store = CredentialManager.Create("Httpflow");
    }

    public Task SaveAsync(string jwt, DateTimeOffset expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(jwt))
        {
            throw new ArgumentException("JWT cannot be empty.", nameof(jwt));
        }

        var session = new StoredJwtSession
        {
            AccessToken = jwt,
            ExpiresAtUtc = expiresAtUtc
        };

        _store.AddOrUpdate(Service, Account, JsonSerializer.Serialize(session));

        return Task.CompletedTask;
    }

    public Task<StoredJwtSession?> GetSessionAsync()
    {
        var credential = _store.Get(Service, Account);
        if (string.IsNullOrWhiteSpace(credential?.Password))
        {
            return Task.FromResult<StoredJwtSession?>(null);
        }

        try
        {
            var session = JsonSerializer.Deserialize<StoredJwtSession>(credential.Password);
            if (session is not null)
            {
                return Task.FromResult<StoredJwtSession?>(session);
            }
        }
        catch (JsonException)
        {
        }

        return Task.FromResult<StoredJwtSession?>(new StoredJwtSession
        {
            AccessToken = credential.Password,
            ExpiresAtUtc = DateTimeOffset.MinValue
        });
    }

    public async Task<string?> GetTokenAsync()
    {
        var session = await GetSessionAsync();
        return session?.AccessToken;
    }

    public Task DeleteAsync()
    {
        _store.Remove(Service, Account);

        return Task.CompletedTask;
    }

    public async Task<bool> HasTokenAsync()
    {
        var token = await GetTokenAsync();

        return !string.IsNullOrWhiteSpace(token);
    }

    public bool IsExpired(StoredJwtSession session)
    {
        return session.ExpiresAtUtc <= DateTimeOffset.UtcNow;
    }
}
