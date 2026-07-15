using CambioReal.Bs2.Auth;

namespace CambioReal.Bs2.Tests.Fakes;

/// <summary>Token fixo, sem chamada de rede — para testes que não exercitam o fluxo de auth em si.</summary>
internal sealed class StubTokenProvider(string token, string tokenType = "Bearer") : IBs2TokenProvider
{
    public ValueTask<(string Token, string TokenType)> GetAccessTokenAsync(
        Bs2Scope scope, string? invalidatedToken, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult((token, tokenType));
}
