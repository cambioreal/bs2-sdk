namespace CambioReal.Bs2.Http;

/// <summary>Modificadores por requisição.</summary>
public sealed record Bs2RequestContext
{
    /// <summary>Contexto vazio.</summary>
    public static Bs2RequestContext Default { get; } = new();

    /// <summary>
    /// Chave de idempotência best-effort, enviada como header <c>Idempotency-Key</c>.
    /// </summary>
    /// <remarks>
    /// O legado real (<c>cerebro/app/Libraries/Bs2/{PixService,PayoutService}.php</c>) **não
    /// envia** esse header — a idempotência de fato vem só do <c>externalId</c> único por
    /// transação (confirmado: grep vazio por qualquer header de idempotência nos dois arquivos).
    /// Este campo existe como proteção defensiva best-effort do lado do chamador, alinhada à
    /// regra canônica do goal-loop ("idempotency key quando suportado"), não como contrato
    /// confirmado da API BS2 — validar quando o provisionamento de escrita sandbox for liberado
    /// (ver discovery.md §8).
    /// </remarks>
    public string? IdempotencyKey { get; init; }
}
