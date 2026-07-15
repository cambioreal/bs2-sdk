namespace CambioReal.Bs2.Models;

/// <summary>
/// Resposta de <c>GET pj/apibanking/forintegration/v2/contascorrentes/saldo</c>
/// (<see cref="Resources.AccountsResource.GetBalanceAsync"/>).
/// </summary>
/// <remarks>
/// <b>Shape NÃO confirmado por fixture/uso no legado</b>: <c>config/bs2-mock.php</c> não tem uma
/// entrada <c>balance</c> (<c>AccountService::balance()</c> aponta para <c>bs2-mock.balance.success</c>,
/// que não existe no arquivo de mocks) e o método nunca é chamado por nenhum outro lugar do
/// <c>cerebro</c> (confirmado por grep em toda a árvore <c>app/</c>). Este shape é inferido do
/// bloco <c>saldo</c> aninhado devolvido dentro de <c>GET .../extrato</c> (mesmo domínio, mesmo
/// nome de campo, ver <see cref="Bs2AccountStatement.Saldo"/>) — tratar como suposição a confirmar
/// quando o endpoint standalone puder ser exercitado ao vivo (ver discovery.md §13).
/// </remarks>
public sealed record Bs2AccountBalance
{
    public decimal Inicial { get; init; }

    public decimal Final { get; init; }

    public Bs2AccountBlockedBalance? Bloqueado { get; init; }
}

/// <summary>Bloco <c>saldo.bloqueado</c> — valores retidos por diferentes motivos regulatórios.</summary>
public sealed record Bs2AccountBlockedBalance
{
    public decimal Por24Horas { get; init; }

    public decimal Por48Horas { get; init; }

    public decimal Acima { get; init; }

    public decimal Judicial { get; init; }
}

/// <summary>
/// Resposta de <c>GET pj/apibanking/forintegration/v2/contascorrentes/extrato</c>
/// (<see cref="Resources.AccountsResource.GetStatementAsync"/>). Confirmado campo a campo em
/// <c>config/bs2-mock.php</c> (chave <c>statement.success</c>) e no uso real de
/// <c>Bs2ReconciliationCommand::fetchBs2Statement</c> (paginação via <see cref="Inicio"/>/
/// <see cref="Total"/>, passo de 100).
/// </summary>
public sealed record Bs2AccountStatement
{
    public Bs2AccountBalance? Saldo { get; init; }

    /// <summary>Sempre <see langword="null"/> observado no legado — shape do cheque especial não confirmado.</summary>
    public object? ChequeEspecial { get; init; }

    public IReadOnlyList<Bs2AccountMovement> Movimentacoes { get; init; } = [];

    /// <summary>Offset enviado na requisição (eco), confirmado no legado.</summary>
    public int Inicio { get; init; }

    /// <summary>Tamanho de página devolvido pela BS2 (não confundir com o passo de 100 hardcoded no legado, que é do chamador).</summary>
    public int Limite { get; init; }

    /// <summary>Total de movimentações no período — usado pelo legado para decidir quando parar de paginar.</summary>
    public int Total { get; init; }
}

/// <summary>
/// Um item de <see cref="Bs2AccountStatement.Movimentacoes"/>. Confirmado no legado
/// (<c>config/bs2-mock.php</c> + <c>Bs2ReconciliationCommand::reconcilePayin/reconcilePayout</c>,
/// que leem <c>valor</c>/<c>remetente.{nome,documento}</c>/<c>favorecido.{nome,documento}</c>/
/// <c>protocolo</c>/<c>descricao</c>/<c>tipoCategoria</c> do payload real).
/// </summary>
public sealed record Bs2AccountMovement
{
    public DateTimeOffset? MovimentadoEm { get; init; }

    public string? Descricao { get; init; }

    public string? DescricaoAbreviada { get; init; }

    public decimal Valor { get; init; }

    /// <summary>
    /// <c>1</c> = débito, <c>2</c> = crédito — confirmado no legado
    /// (<c>Bs2ReconciliationCommand::TIPO_DEBITO</c>/<c>TIPO_CREDITO</c>).
    /// </summary>
    public int TipoMovimentacao { get; init; }

    /// <summary>
    /// Categoria da movimentação. Valores confirmados no legado (não exaustivo — só os usados por
    /// <c>Bs2ReconciliationCommand</c>): <c>2</c>=TED recebido, <c>10</c>=PIX recebido,
    /// <c>11</c>=PIX enviado, <c>16</c>=PIX recebido interno, <c>17</c>=PIX enviado interno,
    /// <c>20</c>=tarifa. Modelado como <see cref="int"/> simples, não enum — catálogo não confirmado
    /// como exaustivo (mesma regra de <see cref="Serialization.Bs2Json"/> para campos de valor fechado).
    /// </summary>
    public int TipoCategoria { get; init; }

    public string? Documento { get; init; }

    public string? Observacao { get; init; }

    public string? Protocolo { get; init; }

    /// <summary>Parte remetente (créditos) — <see langword="null"/> em débitos.</summary>
    public Bs2AccountMovementParty? Remetente { get; init; }

    /// <summary>Parte favorecida (débitos) — <see langword="null"/> em créditos.</summary>
    public Bs2AccountMovementParty? Favorecido { get; init; }

    public Bs2AccountMovementPix? Pix { get; init; }
}

/// <summary>Contraparte (remetente/favorecido) de uma <see cref="Bs2AccountMovement"/>.</summary>
public sealed record Bs2AccountMovementParty
{
    public string? Nome { get; init; }

    public string? Documento { get; init; }

    public string? NomeBanco { get; init; }

    public int? Banco { get; init; }

    public int? Agencia { get; init; }

    public long? Conta { get; init; }
}

/// <summary>Bloco <c>pix</c> de uma <see cref="Bs2AccountMovement"/> — presente mesmo em movimentações não-PIX no fixture do legado (ex.: tarifa).</summary>
public sealed record Bs2AccountMovementPix(string? EndToEndId);
