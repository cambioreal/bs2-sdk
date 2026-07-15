namespace CambioReal.Bs2.Models;

/// <summary>
/// Parte estrangeira de uma transação PIX Câmbio — <c>foreignCreditor</c> no payin,
/// <c>foreignDebtor</c> no payout. Mesmo shape nos dois lados, confirmado no legado.
/// </summary>
public sealed record Bs2ForeignParty(string Country, string Name);

/// <summary>
/// Identificação de conta usada tanto como <c>debtor</c> (payin) quanto como <c>creditor</c> do
/// endpoint account-data (payout) — os dois têm exatamente o mesmo conjunto de campos no legado
/// (<c>PixService::create</c>/<c>PayoutService::createPixByAccount</c>). <c>Cde</c> é sempre
/// <see langword="true"/> no fluxo CDE atual — exposto como propriedade (não constante) só para
/// não fechar a porta a um fluxo não-CDE futuro.
/// </summary>
public sealed record Bs2CdeParty
{
    /// <summary>Código da instituição financeira (config: <c>BS2_FIN_INST</c>).</summary>
    public required string FinancialInstitution { get; init; }

    /// <summary>Emissor/agência (config: <c>BS2_ISSUER</c>).</summary>
    public required string Issuer { get; init; }

    /// <summary>Número da conta (config: <c>BS2_ACCOUNT</c>).</summary>
    public required string Account { get; init; }

    /// <summary>
    /// Tipo de conta. Valores confirmados no legado: <c>"Current"</c>, <c>"Savings"</c> — string
    /// literal, não enum (ver <see cref="Serialization.Bs2Json"/> para o porquê).
    /// </summary>
    public required string AccountType { get; init; }

    /// <summary>Sempre <see langword="true"/> no fluxo CDE atual.</summary>
    public bool Cde { get; init; } = true;

    /// <summary>Código IBAN-like da própria conta BS2 (config: <c>BS2_IBAN_CODE</c>).</summary>
    public required string IbanCode { get; init; }

    /// <summary>CPF/CNPJ da parte (payer no payin, beneficiário no payout account-data).</summary>
    public required string Identification { get; init; }

    /// <summary>
    /// Tipo do documento. Valores confirmados no legado: <c>"CPF"</c>, <c>"CNPJ"</c> — sempre
    /// maiúsculo no request (o mock de resposta mostra <c>"Cnpj"</c>, mas é só formatação de
    /// exibição; não confiar nele para o request — ver discovery.md §10.7).
    /// </summary>
    public required string IdentificationType { get; init; }

    /// <summary>Nome completo/razão social da parte.</summary>
    public required string Name { get; init; }
}

/// <summary>
/// Creditor do endpoint dict-key (payout via chave PIX) — shape muito mais magro que
/// <see cref="Bs2CdeParty"/>: só <c>cde</c>/<c>ibanCode</c>. A chave PIX em si vai em
/// <see cref="Bs2CreditorDict"/>, um objeto irmão, não dentro deste.
/// </summary>
public sealed record Bs2DictKeyCreditor
{
    /// <summary>Sempre <see langword="true"/> no fluxo CDE atual.</summary>
    public bool Cde { get; init; } = true;

    /// <summary>Código IBAN-like da própria conta BS2.</summary>
    public required string IbanCode { get; init; }
}

/// <summary>Chave PIX do beneficiário — <c>creditorDict</c>, usado só no endpoint dict-key.</summary>
public sealed record Bs2CreditorDict(string DictKey, string DictKeyType);

/// <summary>
/// Bloco de classificação regulatória devolvido pela BS2 em detalhes de payin/payout. Campos só
/// de leitura (a BS2 preenche, não enviamos na criação) — confirmado no legado via <c>Arr::get</c>
/// defensivo, nunca setado explicitamente no corpo de request.
/// </summary>
public sealed record Bs2Classification
{
    public string? NatureFact { get; init; }
    public string? ClientCode { get; init; }
    public string? NatureGroup { get; init; }
    public string? NatureGuarantee { get; init; }
    public string? PayerReceiverCode { get; init; }
    public string? OtherEspecifications { get; init; }
}

/// <summary>
/// Envelope de paginação comum a <c>GET collection-orders</c> e <c>GET payment-orders</c>.
/// A chave real é <c>itens</c> (não <c>items</c>) — confirmado no legado; <see cref="Itens"/>
/// serializa/desserializa para essa chave automaticamente via a naming policy camelCase
/// (<c>Itens</c> → <c>itens</c>).
/// </summary>
public sealed record Bs2PagedResult<T>
{
    public int CurrentPage { get; init; }
    public int PageSize { get; init; }
    public int TotalRecords { get; init; }
    public int TotalPages { get; init; }
    public IReadOnlyList<T> Itens { get; init; } = [];
    public int? PreviousPage { get; init; }
    public int? NextPage { get; init; }
}
