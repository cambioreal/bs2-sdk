using CambioReal.Bs2;
using CambioReal.Bs2.Auth;
using CambioReal.Bs2.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace CambioReal.Bs2.SandboxTests;

/// <summary>
/// Integração sandbox real, opt-in — goal-loop §2.5: "nunca rodem por padrão em CI e não
/// imprimam segredos". Credenciais vêm de variáveis de ambiente populadas a partir do
/// <c>pass</c>, nunca hardcoded aqui:
///
/// <code>
/// eval "$(pass show cambio-real-v2/providers/bs2/sandbox-env | sed -n \
///   's/^CLIENT_ID=/BS2_SANDBOX_CLIENT_ID=/p;s/^CLIENT_SECRET=/BS2_SANDBOX_CLIENT_SECRET=/p')"
/// export BS2_SANDBOX_CLIENT_ID BS2_SANDBOX_CLIENT_SECRET
/// dotnet test tests/CambioReal.Bs2.Client.SandboxTests/CambioReal.Bs2.Client.SandboxTests.csproj
/// </code>
///
/// Sem as duas variáveis, os testes falham explicitamente (não pulam em silêncio) — rodar este
/// projeto sem credenciais é um erro do operador, não um caminho suportado. Nenhum teste aqui
/// imprime token, secret ou payload completo — só status HTTP e um resumo saneado.
/// </summary>
public sealed class Bs2SandboxTests
{
    private readonly ITestOutputHelper output;

    public Bs2SandboxTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    [Trait("Category", "Sandbox")]
    public async Task CollectionOrderScope_AuthenticatesLiveAgainstSandbox()
    {
        var (token, tokenType) = await GetLiveTokenAsync(Bs2Scope.CollectionOrder);

        tokenType.ShouldBe("Bearer");
        token.ShouldNotBeNullOrWhiteSpace();
        output.WriteLine($"pix.cambio.collection.order: token issued, length={token.Length} (masked).");
    }

    [Fact]
    [Trait("Category", "Sandbox")]
    public async Task PaymentOrderScope_AuthenticatesLiveAgainstSandbox()
    {
        var (token, tokenType) = await GetLiveTokenAsync(Bs2Scope.PaymentOrder);

        tokenType.ShouldBe("Bearer");
        token.ShouldNotBeNullOrWhiteSpace();
        output.WriteLine($"pix.cambio.payment.order: token issued, length={token.Length} (masked).");
    }

    /// <summary>
    /// Registra o status real do bloqueio de provisionamento documentado em
    /// docs/providers/bs2/discovery.md §3 (confirmado 403 em 2026-07-15). Não assume o status
    /// exato: qualquer resposta HTTP do provedor (2xx/4xx/5xx) conta como sucesso de
    /// conectividade — só falha se a chamada não alcançar a BS2 (erro de rede/protocolo), que é
    /// o sinal real de regressão que este teste existe para capturar.
    /// </summary>
    [Fact]
    [Trait("Category", "Sandbox")]
    public async Task CollectionOrdersList_ReachesProviderAndReportsCurrentAccessStatus()
    {
        using var provider = BuildServiceProvider();
        var client = provider.GetRequiredService<Bs2Client>();

        try
        {
            var page = await client.CollectionOrders.ListAsync(DateOnly.FromDateTime(DateTime.UtcNow));
            output.WriteLine($"GET collection-orders: 200 OK, {page.TotalRecords} registro(s) — bloqueio de provisionamento PARECE RESOLVIDO, expandir cobertura.");
        }
        catch (Bs2ApiException exception)
        {
            output.WriteLine($"GET collection-orders: HTTP {(int)exception.StatusCode} ({exception.StatusCode}) — {Truncate(exception.ErrorCode)}");
        }
    }

    /// <summary>
    /// Espelha <see cref="CollectionOrdersList_ReachesProviderAndReportsCurrentAccessStatus"/> para
    /// o domínio de conta corrente (<c>pj/apibanking/forintegration/v2/contascorrentes/saldo</c>),
    /// achado fora do escopo original da auditoria de payin/payout (ver
    /// docs/providers/bs2/discovery.md §13). Reusa o escopo <c>pix.cambio.collection.order</c> —
    /// mesmo bloqueio de provisionamento (§3) é esperado até desbloqueio externo pela BS2. Nunca
    /// falha por causa do status HTTP em si, só por erro de rede/protocolo.
    /// </summary>
    [Fact]
    [Trait("Category", "Sandbox")]
    public async Task AccountBalance_ReachesProviderAndReportsCurrentAccessStatus()
    {
        using var provider = BuildServiceProvider();
        var client = provider.GetRequiredService<Bs2Client>();

        try
        {
            var balance = await client.Accounts.GetBalanceAsync();
            output.WriteLine($"GET contascorrentes/saldo: 200 OK, final={balance.Final} — bloqueio de provisionamento PARECE RESOLVIDO, expandir cobertura/confirmar shape.");
        }
        catch (Bs2ApiException exception)
        {
            output.WriteLine($"GET contascorrentes/saldo: HTTP {(int)exception.StatusCode} ({exception.StatusCode}) — {Truncate(exception.ErrorCode)}");
        }
    }

    /// <summary>Idem para <c>GET .../contascorrentes/extrato</c> (<see cref="AccountBalance_ReachesProviderAndReportsCurrentAccessStatus"/>).</summary>
    [Fact]
    [Trait("Category", "Sandbox")]
    public async Task AccountStatement_ReachesProviderAndReportsCurrentAccessStatus()
    {
        using var provider = BuildServiceProvider();
        var client = provider.GetRequiredService<Bs2Client>();

        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var statement = await client.Accounts.GetStatementAsync(today, today);
            output.WriteLine($"GET contascorrentes/extrato: 200 OK, {statement.Total} movimentacao(oes) — bloqueio de provisionamento PARECE RESOLVIDO, expandir cobertura.");
        }
        catch (Bs2ApiException exception)
        {
            output.WriteLine($"GET contascorrentes/extrato: HTTP {(int)exception.StatusCode} ({exception.StatusCode}) — {Truncate(exception.ErrorCode)}");
        }
    }

    private static async Task<(string Token, string TokenType)> GetLiveTokenAsync(Bs2Scope scope)
    {
        using var provider = BuildServiceProvider();
        var tokenProvider = provider.GetRequiredService<IBs2TokenProvider>();
        return await tokenProvider.GetAccessTokenAsync(scope, invalidatedToken: null);
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var clientId = Environment.GetEnvironmentVariable("BS2_SANDBOX_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("BS2_SANDBOX_CLIENT_SECRET");

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "BS2_SANDBOX_CLIENT_ID/BS2_SANDBOX_CLIENT_SECRET ausentes — carregue-os de " +
                "`pass cambio-real-v2/providers/bs2/sandbox-env` antes de rodar este projeto. " +
                "Ver o comentário XML no topo de Bs2SandboxTests.cs.");
        }

        var services = new ServiceCollection();
        services.AddBs2Client(options =>
        {
            options.Environment = Bs2Environment.Sandbox;
            options.ClientId = clientId;
            options.ClientSecret = clientSecret;
        });

        return services.BuildServiceProvider();
    }

    private static string Truncate(string? value, int maxLength = 120)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "(sem descrição)";
        }

        return value.Length <= maxLength ? value : value[..maxLength] + "…";
    }
}
