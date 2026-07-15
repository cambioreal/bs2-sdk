using System.Text.Json.Serialization;

namespace CambioReal.Bs2.Auth;

/// <summary>
/// Resposta de <c>POST auth/oauth/v2/token</c>: <c>access_token</c>/<c>token_type</c>/
/// <c>expires_in</c>/<c>scope</c> na raiz, em snake_case — confirmado ao vivo contra o sandbox
/// (2026-07-15, HTTP 200 real para ambos os escopos). Anotado explicitamente com
/// <see cref="JsonPropertyNameAttribute"/> em vez de depender da naming policy de
/// <see cref="Serialization.Bs2Json"/>, que é camelCase para o resto do SDK — o endpoint OAuth2
/// segue a convenção snake_case padrão da RFC 6749, distinta da API REST própria da BS2.
/// </summary>
internal sealed record Bs2TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("scope")] string? Scope);

/// <summary>Token em cache com seu instante de expiração absoluto.</summary>
internal sealed record CachedAccessToken(string Value, string TokenType, DateTimeOffset ExpiresAtUtc);

/// <summary>Nomes dos <c>HttpClient</c> registrados no container.</summary>
internal static class Bs2ClientNames
{
    /// <summary>Cliente dos recursos de payin (<c>collection-orders</c>). Passa pelo handler autenticado com <see cref="Bs2Scope.CollectionOrder"/>.</summary>
    public const string CollectionOrders = "bs2.collection-orders";

    /// <summary>Cliente dos recursos de payout (<c>payment-orders</c>). Passa pelo handler autenticado com <see cref="Bs2Scope.PaymentOrder"/>.</summary>
    public const string PaymentOrders = "bs2.payment-orders";

    /// <summary>
    /// Cliente usado só para <c>POST auth/oauth/v2/token</c>. Precisa ser separado dos clientes de
    /// recurso: se passasse por um handler de autenticação, obter um token exigiria um token.
    /// Mesmo host dos demais — a BS2 não separa host de auth do host de API.
    /// </summary>
    public const string Auth = "bs2.auth";
}
