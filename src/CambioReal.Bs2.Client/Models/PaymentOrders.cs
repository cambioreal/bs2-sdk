namespace CambioReal.Bs2.Models;

/// <summary>
/// Corpo de <c>POST core2/pix/cambio/v1/payment-orders/dict-key</c> (payout via chave PIX) —
/// confirmado no legado (<c>PayoutService::createPixByKey</c>). Note que <see cref="Creditor"/>
/// aqui é o shape magro (<see cref="Bs2DictKeyCreditor"/>, só <c>cde</c>/<c>ibanCode</c>) — a
/// chave PIX fica em <see cref="CreditorDict"/>, um objeto irmão.
/// </summary>
public sealed record CreatePaymentOrderByPixKeyRequest
{
    public required decimal Amount { get; init; }
    public required string ExternalId { get; init; }
    public required string Information { get; init; }
    public int TransactionReason { get; init; } = 1;
    public required string CreditorDebtorType { get; init; }
    public required Bs2DictKeyCreditor Creditor { get; init; }
    public required Bs2CreditorDict CreditorDict { get; init; }
    public required Bs2ForeignParty ForeignDebtor { get; init; }
}

/// <summary>
/// Corpo de <c>POST core2/pix/cambio/v1/payment-orders/account-data</c> (payout via dados
/// bancários) — confirmado no legado (<c>PayoutService::createPixByAccount</c>). **Corrige** a
/// suposição do adapter C# greenfield não confirmado (<c>Bs2SettlementAdapter.cs</c>), que
/// assumia <c>creditor.{name,bankCode,accountNumber,routingNumber,bicCode}</c>: os nomes reais
/// são <see cref="Bs2CdeParty.FinancialInstitution"/>/<see cref="Bs2CdeParty.Issuer"/>/
/// <see cref="Bs2CdeParty.Account"/> — não existe <c>bicCode</c>.
/// </summary>
/// <remarks>
/// Este mesmo endpoint é reusado para **refund** no legado (<c>PixService::refund</c>) — ver
/// <see cref="Resources.PaymentOrdersResource.RefundAsync"/>, que é um alias literal desta chamada.
/// </remarks>
public sealed record CreatePaymentOrderByAccountRequest
{
    public required decimal Amount { get; init; }
    public required string ExternalId { get; init; }
    public required string Information { get; init; }
    public int TransactionReason { get; init; } = 1;
    public required string CreditorDebtorType { get; init; }
    public required Bs2CdeParty Creditor { get; init; }
    public required Bs2ForeignParty ForeignDebtor { get; init; }
}

/// <summary>
/// Resposta de <c>GET core2/pix/cambio/v1/payment-orders/{id}</c>. Não existe endpoint de status
/// agregado por lote — confirmado no legado, cada payment-order tem status individual.
/// </summary>
public sealed record PaymentOrderDetails
{
    public string? Id { get; init; }
    public string? ExternalId { get; init; }
    public DateTimeOffset? CreatedDate { get; init; }
    public Bs2CdeParty? Creditor { get; init; }
    public Bs2CreditorDict? CreditorDict { get; init; }
    public Bs2PaymentOrderTransaction? Transaction { get; init; }
    public Bs2Classification? Classification { get; init; }
    public Bs2ForeignParty? ForeignDebtor { get; init; }
}

/// <summary>
/// Status confirmados no legado (<c>PayoutService::checkPayed</c>): <c>Succeed</c> = completo;
/// <c>Issued</c> = pendente; <c>Initialized</c>/<c>Confirmed</c> = pendente com
/// <c>error='delivered'</c>; <c>Failed</c> = erro. Modelado como <see cref="string"/> simples —
/// mesma razão de <see cref="Bs2CollectionOrderTransaction"/>.
/// </summary>
public sealed record Bs2PaymentOrderTransaction
{
    public DateTimeOffset? PaymentDate { get; init; }
    public decimal Amount { get; init; }
    public string? Information { get; init; }
    public string? EndToEndId { get; init; }
    public string? Status { get; init; }
    public string? StatusInformation { get; init; }

    /// <summary>Confirmado sempre <c>"Creditor"</c> no fluxo de payout atual.</summary>
    public string? PaymentType { get; init; }

    /// <summary>Confirmado <c>"PixByAccountData"</c> no endpoint account-data; não confirmado para dict-key.</summary>
    public string? SettlementType { get; init; }
}
