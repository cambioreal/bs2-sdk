namespace CambioReal.Bs2;

/// <summary>
/// Paths centralizados da API BS2 PIX Câmbio. Cada membro cita o arquivo do legado PHP onde foi
/// confirmado (<c>cerebro/app/Libraries/Bs2/*</c>).
/// </summary>
internal static class Bs2Paths
{
    /// <summary><c>POST auth/oauth/v2/token</c> — <c>AbstractService::authenticate()</c>.</summary>
    public const string Token = "auth/oauth/v2/token";

    /// <summary><c>POST core2/pix/cambio/v1/collection-orders</c> — <c>PixService::create</c>.</summary>
    public const string CollectionOrders = "core2/pix/cambio/v1/collection-orders";

    /// <summary>
    /// <c>GET</c>/<c>DELETE core2/pix/cambio/v1/collection-orders/{orderId}</c> —
    /// <c>PixService::details</c>/<c>PixService::destroy</c>.
    /// </summary>
    public static string CollectionOrder(string orderId) =>
        $"core2/pix/cambio/v1/collection-orders/{Uri.EscapeDataString(orderId)}";

    /// <summary>
    /// <c>GET core2/pix/cambio/v1/collection-orders</c>, paginado. Confirmado no legado: query
    /// <c>DateUtc</c> (yyyy-MM-dd), <c>CurrentPage</c>, <c>QuantityPerPage</c>.
    /// </summary>
    public static string CollectionOrdersList(DateOnly dateUtc, int currentPage, int quantityPerPage) =>
        $"{CollectionOrders}?DateUtc={dateUtc:yyyy-MM-dd}&CurrentPage={currentPage}&QuantityPerPage={quantityPerPage}";

    /// <summary>
    /// <c>POST core2/pix/cambio/v1/payment-orders/dict-key</c> — <c>PayoutService::createPixByKey</c>.
    /// </summary>
    public const string PaymentOrdersDictKey = "core2/pix/cambio/v1/payment-orders/dict-key";

    /// <summary>
    /// <c>POST core2/pix/cambio/v1/payment-orders/account-data</c> —
    /// <c>PayoutService::createPixByAccount</c>. Também usado para refund (<c>PixService::refund</c>).
    /// </summary>
    public const string PaymentOrdersAccountData = "core2/pix/cambio/v1/payment-orders/account-data";

    /// <summary><c>GET core2/pix/cambio/v1/payment-orders/{id}</c> — <c>PayoutService::details</c>.</summary>
    public static string PaymentOrder(string id) =>
        $"core2/pix/cambio/v1/payment-orders/{Uri.EscapeDataString(id)}";

    /// <summary>
    /// <c>GET core2/pix/cambio/v1/payment-orders</c>, paginado — mesmo shape de query do payin.
    /// Não existe endpoint de status agregado por lote; cada payment-order tem status individual.
    /// </summary>
    public static string PaymentOrdersList(DateOnly dateUtc, int currentPage, int quantityPerPage) =>
        $"core2/pix/cambio/v1/payment-orders?DateUtc={dateUtc:yyyy-MM-dd}&CurrentPage={currentPage}&QuantityPerPage={quantityPerPage}";
}
