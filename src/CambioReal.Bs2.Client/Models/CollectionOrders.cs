namespace CambioReal.Bs2.Models;

/// <summary>
/// Corpo de <c>POST core2/pix/cambio/v1/collection-orders</c> (payin) — confirmado campo a campo
/// no legado (<c>PixService::create</c>). A resposta ao POST é uma **string simples** (o id da
/// ordem), não um objeto — por isso não há um <c>CreateCollectionOrderResponse</c>; ver
/// <see cref="Resources.CollectionOrdersResource.CreateAsync"/>.
/// </summary>
public sealed record CreateCollectionOrderRequest
{
    public required decimal Amount { get; init; }

    public required string ExternalId { get; init; }

    public required string Information { get; init; }

    /// <summary>Sempre <c>1</c> no legado — nenhum outro valor foi observado.</summary>
    public int TransactionReason { get; init; } = 1;

    /// <summary>
    /// Tipo de documento do devedor. Valores confirmados no legado: <c>"01"</c> (CPF),
    /// <c>"05"</c> (CNPJ) — string numérica, não int nem enum.
    /// </summary>
    public required string CreditorDebtorType { get; init; }

    public required Bs2CdeParty Debtor { get; init; }

    public required Bs2ForeignParty ForeignCreditor { get; init; }
}

/// <summary>
/// Resposta de <c>GET core2/pix/cambio/v1/collection-orders/{id}</c>. O QR code fica aninhado em
/// <see cref="Transaction"/>.<see cref="Bs2CollectionOrderTransaction.QrCode"/> — não existe no
/// nível raiz. Sem campo <c>expiration</c> — a BS2 não devolve prazo; o legado computa
/// client-side (<c>now()+15min</c>).
/// </summary>
public sealed record CollectionOrderDetails
{
    public string? Id { get; init; }
    public string? ExternalId { get; init; }
    public DateTimeOffset? CreatedDate { get; init; }
    public Bs2CollectionOrderTransaction? Transaction { get; init; }
    public Bs2CdeParty? Debtor { get; init; }
    public Bs2ForeignParty? ForeignCreditor { get; init; }
    public Bs2Classification? Classification { get; init; }
}

/// <summary>
/// Status confirmados no legado (<c>PixService::getStatusPago</c>/<c>PayinNotification::check</c>):
/// <c>Issued</c>, <c>QrCodeGenerated</c> = pendente; <c>Succeed</c> = pago; <c>Failed</c>,
/// <c>RequestedCancel</c>, <c>Canceled</c> = erro/cancelado. Modelado como <see cref="string"/>
/// simples — ver <see cref="Serialization.Bs2Json"/> para o porquê de não usar enum.
/// </summary>
public sealed record Bs2CollectionOrderTransaction
{
    public DateTimeOffset? PaymentDate { get; init; }
    public decimal Amount { get; init; }

    /// <summary>Confirmado sempre <c>"DebtorCDE"</c> no fluxo atual.</summary>
    public string? PaymentType { get; init; }

    public string? Status { get; init; }
    public string? StatusInformation { get; init; }
    public string? Information { get; init; }
    public string? EndToEndId { get; init; }
    public string? QrCode { get; init; }
}
