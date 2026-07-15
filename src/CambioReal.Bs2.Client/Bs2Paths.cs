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

    /// <summary>
    /// <c>GET pj/apibanking/forintegration/v2/contascorrentes/saldo</c> — <c>AccountService::balance</c>.
    /// Domínio distinto (banking/tesouraria, não PIX câmbio) — confirmado no legado
    /// (<c>cerebro/app/Libraries/Bs2/AccountService.php</c>). Sem parâmetros de query.
    /// </summary>
    public const string AccountBalance = "pj/apibanking/forintegration/v2/contascorrentes/saldo";

    /// <summary>
    /// <c>GET pj/apibanking/forintegration/v2/contascorrentes/extrato</c> — <c>AccountService::statement</c>.
    /// Query confirmada no legado: <c>movimentoInicial</c>/<c>movimentoFinal</c> (<c>yyyy-MM-dd</c>),
    /// <c>inicio</c> (offset de paginação — <c>Bs2ReconciliationCommand::fetchBs2Statement</c> incrementa
    /// em passos de 100 até <c>inicio &gt;= total</c>).
    /// </summary>
    public static string AccountStatement(DateOnly startDate, DateOnly endDate, int offset) =>
        $"pj/apibanking/forintegration/v2/contascorrentes/extrato"
        + $"?movimentoInicial={startDate:yyyy-MM-dd}&movimentoFinal={endDate:yyyy-MM-dd}&inicio={offset}";

    /// <summary>
    /// <c>GET pj/apibanking/forintegration/v2/contascorrentes/extrato/analitico</c> —
    /// <c>AccountService::statementV2</c>. Query confirmada no legado: <c>dataInicial</c>/
    /// <c>dataFinal</c> no formato <c>yyyy-MM-dd HH:mm</c> — **com hora**, diferente do
    /// <see cref="AccountStatement"/> (só data). Sem paginação (<c>inicio</c> não existe aqui).
    /// </summary>
    public static string AccountStatementAnalytical(DateTime startDate, DateTime endDate) =>
        $"pj/apibanking/forintegration/v2/contascorrentes/extrato/analitico"
        + $"?dataInicial={Uri.EscapeDataString(startDate.ToString("yyyy-MM-dd HH:mm"))}"
        + $"&dataFinal={Uri.EscapeDataString(endDate.ToString("yyyy-MM-dd HH:mm"))}";
}
