using System.Net;
using CambioReal.Bs2.Tests.Fakes;
using Shouldly;
using Xunit;

namespace CambioReal.Bs2.Tests;

public sealed class AccountsResourceTests
{
    [Fact]
    public async Task GetBalanceAsync_QueriesSaldoPathWithNoQueryString()
    {
        const string json = """{"inicial":296693.12,"final":200847.68,"bloqueado":{"por24Horas":0,"por48Horas":0,"acima":0,"judicial":0}}""";
        var (client, transport) = TestClient.CreateOk(json);

        var balance = await client.Accounts.GetBalanceAsync();

        balance.Inicial.ShouldBe(296693.12m);
        balance.Final.ShouldBe(200847.68m);
        balance.Bloqueado!.Por24Horas.ShouldBe(0m);

        var recorded = transport.Requests.Single();
        recorded.Method.ShouldBe(HttpMethod.Get);
        recorded.RequestUri!.PathAndQuery.ShouldBe("/pj/apibanking/forintegration/v2/contascorrentes/saldo");
    }

    /// <summary>
    /// A conta corrente reusa o MESMO escopo/pipeline HTTP do payin (<c>pix.cambio.collection.order</c>)
    /// — confirmado no legado (<c>AccountService::__construct()</c>). Este teste garante que o
    /// roteamento em <c>Bs2Client.GetAccountsAsync</c> passa pelo handler de auth correto (o mesmo
    /// que já reautentica em 401), não um pipeline sem auth.
    /// </summary>
    [Fact]
    public async Task GetBalanceAsync_RetriesOnceAfter401ThenSucceeds()
    {
        var (client, transport) = TestClient.Create(
            (HttpStatusCode.Unauthorized, "{}"),
            (HttpStatusCode.OK, """{"inicial":1,"final":2}"""));

        var balance = await client.Accounts.GetBalanceAsync();

        balance.Final.ShouldBe(2m);
        transport.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetStatementAsync_BuildsQueryWithDatesAndOffset()
    {
        const string json = """
            {"saldo":{"inicial":1,"final":2},"chequeEspecial":null,"movimentacoes":[],"inicio":0,"limite":100,"total":0}
            """;
        var (client, transport) = TestClient.CreateOk(json);

        await client.Accounts.GetStatementAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 15), offset: 200);

        var recorded = transport.Requests.Single();
        recorded.Method.ShouldBe(HttpMethod.Get);
        recorded.RequestUri!.PathAndQuery.ShouldBe(
            "/pj/apibanking/forintegration/v2/contascorrentes/extrato?movimentoInicial=2026-07-01&movimentoFinal=2026-07-15&inicio=200");
    }

    [Fact]
    public async Task GetStatementAsync_ParsesMovementsWithRemetenteAndFavorecido()
    {
        const string json = """
            {
              "saldo": {"inicial": 296693.12, "final": 200847.68},
              "movimentacoes": [
                {
                  "movimentadoEm": "2026-07-14T10:00:00.000000+00:00",
                  "descricao": "Credito Pix Qr Code",
                  "valor": 275,
                  "tipoMovimentacao": 2,
                  "tipoCategoria": 10,
                  "protocolo": "abc-123",
                  "remetente": {"nome": "Carolina Barreto da Silva", "documento": "48052798850", "nomeBanco": "Nu Pagamentos SA", "banco": 260, "agencia": 1, "conta": 906384383},
                  "favorecido": null,
                  "pix": {"endToEndId": "E123"}
                },
                {
                  "movimentadoEm": "2026-07-14T11:00:00.000000+00:00",
                  "descricao": "Debito Pix",
                  "valor": 825,
                  "tipoMovimentacao": 1,
                  "tipoCategoria": 11,
                  "protocolo": "def-456",
                  "remetente": null,
                  "favorecido": {"nome": "Fernando da Silva", "documento": "16919700182"},
                  "pix": {"endToEndId": "E456"}
                }
              ],
              "inicio": 0,
              "limite": 100,
              "total": 2
            }
            """;
        var (client, _) = TestClient.CreateOk(json);

        var statement = await client.Accounts.GetStatementAsync(new DateOnly(2026, 7, 14), new DateOnly(2026, 7, 14));

        statement.Total.ShouldBe(2);
        statement.Movimentacoes.Count.ShouldBe(2);

        var credit = statement.Movimentacoes[0];
        credit.TipoMovimentacao.ShouldBe(2);
        credit.Remetente!.Nome.ShouldBe("Carolina Barreto da Silva");
        credit.Favorecido.ShouldBeNull();

        var debit = statement.Movimentacoes[1];
        debit.TipoMovimentacao.ShouldBe(1);
        debit.Favorecido!.Nome.ShouldBe("Fernando da Silva");
        debit.Remetente.ShouldBeNull();
    }

    /// <summary>Espelha <c>Bs2ReconciliationCommand::fetchBs2Statement</c>: passo de 100, para quando <c>offset &gt;= total</c>.</summary>
    [Fact]
    public async Task GetFullStatementAsync_PaginatesUntilOffsetReachesTotal()
    {
        var (client, transport) = TestClient.Create(
            (HttpStatusCode.OK, PageJson(count: 100, total: 250, inicio: 0)),
            (HttpStatusCode.OK, PageJson(count: 100, total: 250, inicio: 100)),
            (HttpStatusCode.OK, PageJson(count: 50, total: 250, inicio: 200)));

        var movements = await client.Accounts.GetFullStatementAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 15));

        movements.Count.ShouldBe(250);
        transport.Requests.Count.ShouldBe(3);
        transport.Requests[0].RequestUri!.Query.ShouldContain("inicio=0");
        transport.Requests[1].RequestUri!.Query.ShouldContain("inicio=100");
        transport.Requests[2].RequestUri!.Query.ShouldContain("inicio=200");
    }

    [Fact]
    public async Task GetFullStatementAsync_StopsWhenPageComesBackEmptyEvenIfTotalNotReached()
    {
        var (client, transport) = TestClient.Create(
            (HttpStatusCode.OK, PageJson(count: 0, total: 250, inicio: 0)));

        var movements = await client.Accounts.GetFullStatementAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 15));

        movements.Count.ShouldBe(0);
        transport.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetStatementAnalyticalAsync_BuildsQueryWithDateAndTimeEscaped()
    {
        var (client, transport) = TestClient.CreateOk("""{"anything":"goes"}""");

        var raw = await client.Accounts.GetStatementAnalyticalAsync(
            new DateTime(2026, 7, 1, 8, 30, 0), new DateTime(2026, 7, 15, 18, 0, 0));

        raw.GetProperty("anything").GetString().ShouldBe("goes");

        var recorded = transport.Requests.Single();
        recorded.RequestUri!.PathAndQuery.ShouldBe(
            "/pj/apibanking/forintegration/v2/contascorrentes/extrato/analitico"
            + "?dataInicial=2026-07-01%2008%3A30&dataFinal=2026-07-15%2018%3A00");
    }

    private static string PageJson(int count, int total, int inicio)
    {
        var items = string.Join(',', Enumerable.Range(0, count).Select(_ => """
            {"valor":1,"tipoMovimentacao":2,"tipoCategoria":10,"protocolo":"p"}
            """));

        return $$"""{"saldo":{"inicial":1,"final":2},"movimentacoes":[{{items}}],"inicio":{{inicio}},"limite":100,"total":{{total}}}""";
    }
}
