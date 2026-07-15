using CambioReal.Bs2.Http;
using CambioReal.Bs2.Models;

namespace CambioReal.Bs2.Resources;

/// <summary>Payin — ordens de cobrança PIX. <c>core2/pix/cambio/v1/collection-orders</c>.</summary>
public sealed class CollectionOrdersResource
{
    private readonly Bs2Client client;

    internal CollectionOrdersResource(Bs2Client client) => this.client = client;

    /// <summary>
    /// Cria uma ordem de cobrança. <c>POST core2/pix/cambio/v1/collection-orders</c>. A resposta é
    /// uma string simples (o id da ordem) — confirmado no legado (<c>PixService::create</c>), não
    /// um objeto JSON, ao contrário da suposição do adapter C# greenfield não confirmado.
    /// </summary>
    public Task<string> CreateAsync(
        CreateCollectionOrderRequest request,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default) =>
        client.PostCollectionOrdersAsync<CreateCollectionOrderRequest, string>(
            Bs2Paths.CollectionOrders,
            request,
            idempotencyKey is null ? null : new Bs2RequestContext { IdempotencyKey = idempotencyKey },
            cancellationToken);

    /// <summary>
    /// Consulta detalhes/status de uma ordem de cobrança, incluindo o QR code
    /// (<c>transaction.qrCode</c>). <c>GET core2/pix/cambio/v1/collection-orders/{orderId}</c>.
    /// </summary>
    public Task<CollectionOrderDetails> GetAsync(string orderId, CancellationToken cancellationToken = default) =>
        client.GetCollectionOrdersAsync<CollectionOrderDetails>(Bs2Paths.CollectionOrder(orderId), cancellationToken);

    /// <summary>
    /// Lista ordens de cobrança paginadas. <c>GET core2/pix/cambio/v1/collection-orders</c>.
    /// Resposta usa a chave <c>itens</c> (não <c>items</c>) — confirmado no legado.
    /// </summary>
    public Task<Bs2PagedResult<CollectionOrderDetails>> ListAsync(
        DateOnly dateUtc, int currentPage = 1, int quantityPerPage = 20, CancellationToken cancellationToken = default) =>
        client.GetCollectionOrdersAsync<Bs2PagedResult<CollectionOrderDetails>>(
            Bs2Paths.CollectionOrdersList(dateUtc, currentPage, quantityPerPage), cancellationToken);

    /// <summary>
    /// Cancela uma ordem de cobrança (QR code expirado). <c>DELETE core2/pix/cambio/v1/collection-orders/{orderId}</c>.
    /// Efeito destrutivo — só executar contra sandbox real com autorização explícita.
    /// </summary>
    public Task CancelAsync(string orderId, CancellationToken cancellationToken = default) =>
        client.DeleteCollectionOrdersAsync(Bs2Paths.CollectionOrder(orderId), cancellationToken);

    /// <summary>
    /// Cria a ordem e aguarda o QR code ficar disponível — espelha <c>PixService::createRaw</c>
    /// (10 tentativas / 1s hardcoded no legado; aqui parametrizado, mesmo default via
    /// <see cref="Bs2Options.CollectionOrderPollTries"/>/<see cref="Bs2Options.CollectionOrderPollDelay"/>).
    /// </summary>
    public async Task<CollectionOrderDetails> CreateAndWaitForQrCodeAsync(
        CreateCollectionOrderRequest request,
        int maxTries,
        TimeSpan delay,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var orderId = await CreateAsync(request, idempotencyKey, cancellationToken);
        return await PollForQrCodeAsync(orderId, maxTries, delay, cancellationToken);
    }

    /// <summary>
    /// Repete <see cref="GetAsync"/> até obter <c>transaction.qrCode</c> ou status <c>Failed</c>,
    /// o que vier primeiro. Também é o mecanismo recomendado para reagir a um webhook de payin:
    /// o payload do webhook não é fonte confiável de status (ver discovery.md §6) — o padrão
    /// canônico é usá-lo só como gatilho e sempre re-consultar aqui.
    /// </summary>
    public async Task<CollectionOrderDetails> PollForQrCodeAsync(
        string orderId, int maxTries, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        var tries = Math.Max(1, maxTries);

        for (var attempt = 1; attempt <= tries; attempt++)
        {
            var details = await GetAsync(orderId, cancellationToken);
            var status = details.Transaction?.Status;

            if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                return details;
            }

            if (!string.IsNullOrWhiteSpace(details.Transaction?.QrCode))
            {
                return details;
            }

            if (attempt < tries)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }

        return await GetAsync(orderId, cancellationToken);
    }
}
