using System.Collections.Concurrent;
using System.Net.Http.Json;
using CambioReal.Bs2.Http;
using CambioReal.Bs2.Serialization;
using Microsoft.Extensions.Options;

namespace CambioReal.Bs2.Auth;

/// <summary>
/// Cacheia os tokens OAuth2 da BS2 (um por <see cref="Bs2Scope"/>) e os renova sob demanda.
/// </summary>
/// <remarks>
/// Espelha <c>CambioReal.Ripple.Auth.RippleTokenProvider</c> do ripple-sdk: singleton,
/// single-flight por escopo (uma rajada de 401 concorrentes para o mesmo escopo produz uma
/// reautenticação, não N). A diferença estrutural é o cache/gate ficarem chaveados por
/// <see cref="Bs2Scope"/>, já que a BS2 — diferente da Ripple/Kira — exige um token por escopo.
/// </remarks>
internal sealed class Bs2TokenProvider : IBs2TokenProvider, IDisposable
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly Bs2Options options;
    private readonly TimeProvider timeProvider;
    private readonly ConcurrentDictionary<Bs2Scope, SemaphoreSlim> refreshGates = new();
    private readonly ConcurrentDictionary<Bs2Scope, CachedAccessToken> cachedTokens = new();

    public Bs2TokenProvider(IHttpClientFactory httpClientFactory, IOptions<Bs2Options> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.httpClientFactory = httpClientFactory;
        this.options = options.Value;
        this.timeProvider = timeProvider;
    }

    public async ValueTask<(string Token, string TokenType)> GetAccessTokenAsync(
        Bs2Scope scope, string? invalidatedToken, CancellationToken cancellationToken = default)
    {
        if (TryUseCached(scope, invalidatedToken, out var token, out var tokenType))
        {
            return (token, tokenType);
        }

        var gate = refreshGates.GetOrAdd(scope, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (TryUseCached(scope, invalidatedToken, out token, out tokenType))
            {
                return (token, tokenType);
            }

            var fresh = await RequestTokenAsync(scope, cancellationToken);
            cachedTokens[scope] = fresh;
            return (fresh.Value, fresh.TokenType);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        foreach (var gate in refreshGates.Values)
        {
            gate.Dispose();
        }
    }

    private bool TryUseCached(Bs2Scope scope, string? invalidatedToken, out string token, out string tokenType)
    {
        token = string.Empty;
        tokenType = string.Empty;

        if (!cachedTokens.TryGetValue(scope, out var current))
        {
            return false;
        }

        if (invalidatedToken is not null && string.Equals(current.Value, invalidatedToken, StringComparison.Ordinal))
        {
            return false;
        }

        if (timeProvider.GetUtcNow() >= current.ExpiresAtUtc)
        {
            return false;
        }

        token = current.Value;
        tokenType = current.TokenType;
        return true;
    }

    private async Task<CachedAccessToken> RequestTokenAsync(Bs2Scope scope, CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient(Bs2ClientNames.Auth);

        // Confirmado no legado (AbstractService::authenticate(), Http::asForm()): form-urlencoded,
        // credenciais SEMPRE no corpo — sem Authorization: Basic (diferente da Ripple, que duplica
        // client_id/client_secret no corpo JSON *e* manda Basic).
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["scope"] = scope.ToScopeString(),
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Bs2Paths.Token, UriKind.Relative))
        {
            Content = content,
        };

        var issuedAt = timeProvider.GetUtcNow();

        using var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Bs2AuthenticationException(
                response.StatusCode,
                errorCode: null,
                $"Falha ao autenticar na BS2 para o escopo '{scope.ToScopeString()}' (HTTP {(int)response.StatusCode}).",
                errorBody);
        }

        var payload = await response.Content.ReadFromJsonAsync<Bs2TokenResponse>(Bs2Json.Options, cancellationToken)
            ?? throw new Bs2AuthenticationException($"A BS2 devolveu um corpo vazio em POST {Bs2Paths.Token}.");

        if (string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new Bs2AuthenticationException("A BS2 devolveu um access_token vazio.");
        }

        var lifetime = TimeSpan.FromSeconds(payload.ExpiresIn);
        var skew = options.TokenExpirationSkew;

        if (skew >= lifetime)
        {
            skew = TimeSpan.FromTicks(lifetime.Ticks / 2);
        }

        var tokenType = string.IsNullOrWhiteSpace(payload.TokenType) ? "Bearer" : payload.TokenType;

        return new CachedAccessToken(payload.AccessToken, tokenType, issuedAt + lifetime - skew);
    }
}
