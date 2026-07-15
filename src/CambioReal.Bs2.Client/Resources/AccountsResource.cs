using System.Text.Json;
using CambioReal.Bs2.Models;

namespace CambioReal.Bs2.Resources;

/// <summary>
/// Conta corrente — consulta de saldo/extrato. Domínio banking/tesouraria
/// (<c>pj/apibanking/forintegration/v2/contascorrentes</c>), distinto do domínio PIX câmbio
/// (<c>core2/pix/cambio/v1/*</c>) exposto por <see cref="CollectionOrdersResource"/>/
/// <see cref="PaymentOrdersResource"/>.
/// </summary>
/// <remarks>
/// Confirmado no legado (<c>cerebro/app/Libraries/Bs2/AccountService.php</c>), usado hoje por
/// <c>Bs2ReconciliationCommand</c> (concilia payin/payout contra o extrato bancário) e por
/// <c>Bs2BankFeeAccountingCommand</c>/<c>Bs2MarlimAccountingCommand</c> (contabilização de tarifas).
/// Achado fora do escopo original da auditoria de payin/payout/webhooks
/// (<c>provider-protocol/docs/gateways/coverage/bs2.md</c>) — não tem endpoint de auth próprio: o
/// legado (<c>AccountService::__construct()</c>) fixa <c>$this->scope = 'pix.cambio.collection.order'</c>,
/// o MESMO escopo do payin. Por isso este recurso reusa o pipeline HTTP de
/// <see cref="CollectionOrdersResource"/> (<c>Bs2Client.GetAccountsAsync</c>), sem exigir um
/// terceiro <see cref="HttpClient"/>/escopo OAuth2.
/// </remarks>
public sealed class AccountsResource
{
    private readonly Bs2Client client;

    internal AccountsResource(Bs2Client client) => this.client = client;

    /// <summary>
    /// Consulta o saldo da conta corrente. <c>GET .../contascorrentes/saldo</c>. Sem parâmetros.
    /// Shape de resposta inferido, não confirmado por fixture/uso real — ver
    /// <see cref="Bs2AccountBalance"/>.
    /// </summary>
    public Task<Bs2AccountBalance> GetBalanceAsync(CancellationToken cancellationToken = default) =>
        client.GetAccountsAsync<Bs2AccountBalance>(Bs2Paths.AccountBalance, cancellationToken);

    /// <summary>
    /// Consulta uma página do extrato da conta corrente. <c>GET .../contascorrentes/extrato</c>.
    /// Confirmado campo a campo no legado — ver <see cref="Bs2AccountStatement"/>. Para buscar o
    /// período inteiro sem gerenciar o offset manualmente, ver <see cref="GetFullStatementAsync"/>.
    /// </summary>
    public Task<Bs2AccountStatement> GetStatementAsync(
        DateOnly startDate, DateOnly endDate, int offset = 0, CancellationToken cancellationToken = default) =>
        client.GetAccountsAsync<Bs2AccountStatement>(
            Bs2Paths.AccountStatement(startDate, endDate, offset), cancellationToken);

    /// <summary>
    /// Busca o extrato completo do período, paginando automaticamente via <c>inicio</c> até
    /// esgotar <c>total</c> — espelha <c>Bs2ReconciliationCommand::fetchBs2Statement</c> (passo de
    /// 100, mesmo default hardcoded no legado; aqui parametrizável). Não replica o retry-com-sleep
    /// em resposta vazia do legado — isso é orquestração de job de conciliação, não responsabilidade
    /// do SDK (mesma decisão já tomada para não replicar a resolução de external-id por prefixo do
    /// webhook, ver discovery.md §6).
    /// </summary>
    public async Task<IReadOnlyList<Bs2AccountMovement>> GetFullStatementAsync(
        DateOnly startDate, DateOnly endDate, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, $"{nameof(pageSize)} precisa ser >= 1.");
        }

        var movements = new List<Bs2AccountMovement>();
        var offset = 0;

        while (true)
        {
            var page = await GetStatementAsync(startDate, endDate, offset, cancellationToken);
            movements.AddRange(page.Movimentacoes);

            offset += pageSize;

            if (page.Movimentacoes.Count == 0 || offset >= page.Total)
            {
                break;
            }
        }

        return movements;
    }

    /// <summary>
    /// Consulta o extrato analítico da conta corrente. <c>GET .../contascorrentes/extrato/analitico</c>.
    /// </summary>
    /// <remarks>
    /// <b>Shape de resposta NÃO confirmado</b>: sem entrada em <c>config/bs2-mock.php</c>
    /// (<c>AccountService::statementV2()</c> aponta para <c>bs2-mock.statementV2.success</c>, que
    /// não existe) e sem nenhuma chamada a este método em outro lugar do <c>cerebro</c> (confirmado
    /// por grep). Devolvido cru como <see cref="JsonElement"/> — não modelar um schema forte sem
    /// evidência real, mesmo padrão de campo "cru" já usado no <c>ouribank-sdk</c>
    /// (<c>Models/Ouribank.cs</c>, <c>JsonElement</c>) quando a forma do provider é desconhecida.
    /// Validar e tipar quando o endpoint puder ser exercitado ao vivo.
    /// </remarks>
    public Task<JsonElement> GetStatementAnalyticalAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) =>
        client.GetAccountsAsync<JsonElement>(
            Bs2Paths.AccountStatementAnalytical(startDate, endDate), cancellationToken);
}
