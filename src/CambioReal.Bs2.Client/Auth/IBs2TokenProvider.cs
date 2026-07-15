namespace CambioReal.Bs2.Auth;

/// <summary>Fornece tokens OAuth2 de acesso por escopo, com cache.</summary>
public interface IBs2TokenProvider
{
    /// <summary>
    /// Devolve um token válido para <paramref name="scope"/>, reautenticando se necessário.
    /// </summary>
    /// <param name="scope">Escopo do token — a BS2 exige um token por escopo, não um único token universal.</param>
    /// <param name="invalidatedToken">
    /// O token que acabou de ser rejeitado com 401, ou <see langword="null"/> numa chamada normal.
    /// Quando informado, o provedor só reautentica se o valor em cache (para esse escopo) ainda for
    /// esse mesmo token — assim, várias requisições que tomam 401 em paralelo para o mesmo escopo
    /// compartilham uma única reautenticação.
    /// </param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>O token de acesso e seu esquema (ex.: <c>Bearer</c>).</returns>
    public ValueTask<(string Token, string TokenType)> GetAccessTokenAsync(
        Bs2Scope scope, string? invalidatedToken, CancellationToken cancellationToken = default);
}
