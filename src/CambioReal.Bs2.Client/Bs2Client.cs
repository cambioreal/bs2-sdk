using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CambioReal.Bs2.Http;
using CambioReal.Bs2.Resources;
using CambioReal.Bs2.Serialization;

namespace CambioReal.Bs2;

/// <summary>
/// Cliente HTTP da API BS2 PIX Câmbio.
/// </summary>
/// <remarks>
/// Camada de transporte, no mesmo espírito do <c>RippleClient</c>/<c>KiraClient</c>. A diferença
/// estrutural: a BS2 exige um <see cref="HttpClient"/> por escopo OAuth2 (um para
/// <see cref="CollectionOrders"/>, outro para <see cref="PaymentOrders"/>) em vez de um único
/// cliente universal — cada um passa por um <c>Bs2AuthenticationHandler</c> configurado com o
/// escopo correspondente (ver <see cref="Bs2ServiceCollectionExtensions"/>).
/// </remarks>
public sealed class Bs2Client
{
    private readonly HttpClient collectionOrdersHttpClient;
    private readonly HttpClient paymentOrdersHttpClient;

    /// <summary>Cria o cliente sobre dois <see cref="HttpClient"/> já configurados (um por escopo).</summary>
    public Bs2Client(HttpClient collectionOrdersHttpClient, HttpClient paymentOrdersHttpClient)
    {
        ArgumentNullException.ThrowIfNull(collectionOrdersHttpClient);
        ArgumentNullException.ThrowIfNull(paymentOrdersHttpClient);

        this.collectionOrdersHttpClient = collectionOrdersHttpClient;
        this.paymentOrdersHttpClient = paymentOrdersHttpClient;

        CollectionOrders = new CollectionOrdersResource(this);
        PaymentOrders = new PaymentOrdersResource(this);
        Accounts = new AccountsResource(this);
    }

    /// <summary>Payin — ordens de cobrança. <c>core2/pix/cambio/v1/collection-orders</c>.</summary>
    public CollectionOrdersResource CollectionOrders { get; }

    /// <summary>Payout — ordens de pagamento. <c>core2/pix/cambio/v1/payment-orders</c>.</summary>
    public PaymentOrdersResource PaymentOrders { get; }

    /// <summary>Conta corrente — saldo/extrato. <c>pj/apibanking/forintegration/v2/contascorrentes</c>.</summary>
    public AccountsResource Accounts { get; }

    internal Task<TResponse> GetCollectionOrdersAsync<TResponse>(string path, CancellationToken cancellationToken) =>
        GetAsync<TResponse>(collectionOrdersHttpClient, path, cancellationToken);

    /// <summary>
    /// Roteia consultas de conta corrente pelo MESMO pipeline HTTP de <see cref="CollectionOrders"/>
    /// — confirmado no legado que <c>contascorrentes/*</c> usa o escopo <c>pix.cambio.collection.order</c>,
    /// não um escopo/HttpClient próprio (ver <see cref="Resources.AccountsResource"/>).
    /// </summary>
    internal Task<TResponse> GetAccountsAsync<TResponse>(string path, CancellationToken cancellationToken) =>
        GetAsync<TResponse>(collectionOrdersHttpClient, path, cancellationToken);

    internal Task<TResponse> PostCollectionOrdersAsync<TRequest, TResponse>(
        string path, TRequest body, Bs2RequestContext? context, CancellationToken cancellationToken) =>
        PostAsync<TRequest, TResponse>(collectionOrdersHttpClient, path, body, context, cancellationToken);

    internal Task DeleteCollectionOrdersAsync(string path, CancellationToken cancellationToken) =>
        DeleteAsync(collectionOrdersHttpClient, path, cancellationToken);

    internal Task<TResponse> GetPaymentOrdersAsync<TResponse>(string path, CancellationToken cancellationToken) =>
        GetAsync<TResponse>(paymentOrdersHttpClient, path, cancellationToken);

    internal Task<TResponse> PostPaymentOrdersAsync<TRequest, TResponse>(
        string path, TRequest body, Bs2RequestContext? context, CancellationToken cancellationToken) =>
        PostAsync<TRequest, TResponse>(paymentOrdersHttpClient, path, body, context, cancellationToken);

    private static async Task<TResponse> GetAsync<TResponse>(HttpClient httpClient, string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path, Bs2RequestContext.Default, content: null);
        return await SendAndReadAsync<TResponse>(httpClient, request, cancellationToken);
    }

    private static async Task<TResponse> PostAsync<TRequest, TResponse>(
        HttpClient httpClient, string path, TRequest body, Bs2RequestContext? context, CancellationToken cancellationToken)
    {
        var content = JsonContent.Create(body, options: Bs2Json.Options);
        using var request = CreateRequest(HttpMethod.Post, path, context ?? Bs2RequestContext.Default, content);
        return await SendAndReadAsync<TResponse>(httpClient, request, cancellationToken);
    }

    private static async Task DeleteAsync(HttpClient httpClient, string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Delete, path, Bs2RequestContext.Default, content: null);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await ThrowIfUnsuccessfulAsync(response, cancellationToken);
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        Bs2RequestContext context,
        HttpContent? content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(context);

        if (path.StartsWith('/'))
        {
            throw new ArgumentException(
                $"O path deve ser relativo e não pode começar com '/'. Recebido: '{path}'.",
                nameof(path));
        }

        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative))
        {
            Content = content,
        };

        if (!string.IsNullOrEmpty(context.IdempotencyKey))
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", context.IdempotencyKey);
        }

        return request;
    }

    private static async Task<TResponse> SendAndReadAsync<TResponse>(
        HttpClient httpClient, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await ThrowIfUnsuccessfulAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<TResponse>(Bs2Json.Options, cancellationToken);

        return payload ?? throw new Bs2ApiException(
            response.StatusCode,
            errorCode: null,
            "A API BS2 devolveu um corpo JSON vazio onde um valor era esperado.",
            responseBody: null);
    }

    private static async Task ThrowIfUnsuccessfulAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var errorCode = TryExtractErrorCode(body);
        var message = $"A API BS2 respondeu HTTP {(int)response.StatusCode} ({response.StatusCode})"
            + (errorCode is null ? "." : $": {errorCode}.");

        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new Bs2AuthenticationException(response.StatusCode, errorCode, message, body),
            _ => new Bs2ApiException(response.StatusCode, errorCode, message, body),
        };
    }

    /// <summary>
    /// Extrai a descrição de erro do corpo. Confirmado no legado (<c>AbstractService::request()</c>
    /// + fixtures de falha de <c>config/bs2-mock.php</c>) — a BS2 usa **duas formas distintas** de
    /// corpo de erro, nenhum catálogo de código máquina-legível: um objeto <c>{message}</c>, ou um
    /// array na raiz com <c>{tag, descricao}</c> (chave em português) ou <c>{description}</c>.
    /// </summary>
    private static string? TryExtractErrorCode(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var first = root[0];

                if (first.TryGetProperty("tag", out var tag) && tag.ValueKind == JsonValueKind.String
                    && first.TryGetProperty("descricao", out var descricao) && descricao.ValueKind == JsonValueKind.String)
                {
                    return $"{tag.GetString()}: {descricao.GetString()}";
                }

                if (first.TryGetProperty("descricao", out var descricaoOnly) && descricaoOnly.ValueKind == JsonValueKind.String)
                {
                    return descricaoOnly.GetString();
                }

                if (first.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String)
                {
                    return description.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // Corpo não-JSON — status e corpo bruto já vão na exceção.
        }

        return null;
    }
}
