using CambioReal.Bs2.Http;
using CambioReal.Bs2.Models;

namespace CambioReal.Bs2.Resources;

/// <summary>Payout — ordens de pagamento PIX. <c>core2/pix/cambio/v1/payment-orders</c>.</summary>
public sealed class PaymentOrdersResource
{
    private readonly Bs2Client client;

    internal PaymentOrdersResource(Bs2Client client) => this.client = client;

    /// <summary>
    /// Cria um payout via chave PIX. <c>POST core2/pix/cambio/v1/payment-orders/dict-key</c>.
    /// Assume-se resposta em string simples (o id da ordem), espelhando o payin — não confirmado
    /// independentemente para payment-orders; validar quando o provisionamento de escrita sandbox
    /// for liberado (ver discovery.md §10.5).
    /// </summary>
    public Task<string> CreateByPixKeyAsync(
        CreatePaymentOrderByPixKeyRequest request,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default) =>
        client.PostPaymentOrdersAsync<CreatePaymentOrderByPixKeyRequest, string>(
            Bs2Paths.PaymentOrdersDictKey,
            request,
            idempotencyKey is null ? null : new Bs2RequestContext { IdempotencyKey = idempotencyKey },
            cancellationToken);

    /// <summary>
    /// Cria um payout via dados bancários. <c>POST core2/pix/cambio/v1/payment-orders/account-data</c>.
    /// Campos corrigidos vs. a suposição do adapter C# greenfield — ver
    /// <see cref="Models.CreatePaymentOrderByAccountRequest"/>.
    /// </summary>
    public Task<string> CreateByAccountAsync(
        CreatePaymentOrderByAccountRequest request,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default) =>
        client.PostPaymentOrdersAsync<CreatePaymentOrderByAccountRequest, string>(
            Bs2Paths.PaymentOrdersAccountData,
            request,
            idempotencyKey is null ? null : new Bs2RequestContext { IdempotencyKey = idempotencyKey },
            cancellationToken);

    /// <summary>
    /// Reembolso — reusa o MESMO endpoint account-data (confirmado no legado,
    /// <c>PixService::refund</c>), não um endpoint dedicado. Exposto como método separado só por
    /// clareza de API do lado do gateway; a chamada de fio é idêntica a
    /// <see cref="CreateByAccountAsync"/>.
    /// </summary>
    public Task<string> RefundAsync(
        CreatePaymentOrderByAccountRequest request,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default) =>
        CreateByAccountAsync(request, idempotencyKey, cancellationToken);

    /// <summary>
    /// Consulta status de uma ordem de pagamento individual. <c>GET core2/pix/cambio/v1/payment-orders/{id}</c>.
    /// Não existe endpoint de status agregado por lote.
    /// </summary>
    public Task<PaymentOrderDetails> GetAsync(string paymentOrderId, CancellationToken cancellationToken = default) =>
        client.GetPaymentOrdersAsync<PaymentOrderDetails>(Bs2Paths.PaymentOrder(paymentOrderId), cancellationToken);

    /// <summary>
    /// Lista ordens de pagamento paginadas. <c>GET core2/pix/cambio/v1/payment-orders</c>. Mesmo
    /// shape paginado do payin (chave <c>itens</c>).
    /// </summary>
    public Task<Bs2PagedResult<PaymentOrderDetails>> ListAsync(
        DateOnly dateUtc, int currentPage = 1, int quantityPerPage = 20, CancellationToken cancellationToken = default) =>
        client.GetPaymentOrdersAsync<Bs2PagedResult<PaymentOrderDetails>>(
            Bs2Paths.PaymentOrdersList(dateUtc, currentPage, quantityPerPage), cancellationToken);
}
