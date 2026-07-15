using System.Net;
using System.Net.Http.Headers;
using CambioReal.Bs2.Http;

namespace CambioReal.Bs2.Auth;

/// <summary>
/// Injeta <c>Authorization: {token_type} {access_token}</c> em toda requisição usando o token do
/// <see cref="Bs2Scope"/> fixado na construção, reautenticando uma única vez diante de um 401.
/// </summary>
internal sealed class Bs2AuthenticationHandler : DelegatingHandler
{
    private readonly IBs2TokenProvider tokenProvider;
    private readonly Bs2Scope scope;

    public Bs2AuthenticationHandler(IBs2TokenProvider tokenProvider, Bs2Scope scope)
    {
        ArgumentNullException.ThrowIfNull(tokenProvider);
        this.tokenProvider = tokenProvider;
        this.scope = scope;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (token, tokenType) = await tokenProvider.GetAccessTokenAsync(scope, invalidatedToken: null, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue(tokenType, token);

        // A cópia precisa existir antes do envio — depois dele o Content já foi descartado.
        var retry = await request.CloneAsync(cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            retry.Dispose();
            return response;
        }

        response.Dispose();

        var (refreshedToken, refreshedTokenType) = await tokenProvider.GetAccessTokenAsync(scope, invalidatedToken: token, cancellationToken);
        retry.Headers.Authorization = new AuthenticationHeaderValue(refreshedTokenType, refreshedToken);

        return await base.SendAsync(retry, cancellationToken);
    }
}
