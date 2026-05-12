using System;
using System.Text.Json;
using System.Threading.Tasks;
using GitCredentialManager;
using Httpflow.Desktop.Models;

namespace Httpflow.Desktop.Services;

public sealed class JwtService
{
    private const string Service = "https://httpflow.local/auth";
    private const string Account = "jwt";

    private readonly ICredentialStore _store;

    public JwtService()
    {
        _store = CredentialManager.Create("Httpflow");
    }

    public Task SaveAsync(string jwt, DateTimeOffset expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(jwt))
        {
            throw new ArgumentException("JWT cannot be empty.", nameof(jwt));
        }

        var session = new StoredSession
        {
            AccessToken = jwt,
            ExpiresAtUtc = expiresAtUtc
        };

        _store.AddOrUpdate(Service, Account, JsonSerializer.Serialize(session));

        return Task.CompletedTask;
    }

    public Task<StoredSession?> GetSessionAsync()
    {
        var credential = _store.Get(Service, Account);
        if (string.IsNullOrWhiteSpace(credential?.Password))
        {
            return Task.FromResult<StoredSession?>(null);
        }

        try
        {
            var session = JsonSerializer.Deserialize<StoredSession>(credential.Password);
            if (session is not null)
            {
                return Task.FromResult<StoredSession?>(session);
            }
        }
        catch (JsonException)
        {
        }

        return Task.FromResult<StoredSession?>(new StoredSession
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

    public bool IsExpired(StoredSession session)
    {
        return session.ExpiresAtUtc <= DateTimeOffset.UtcNow;
    }
}
