namespace CambioReal.Bs2.Models;

/// <summary>
/// Taxonomia canônica dos status de <c>collection-order</c> da BS2.
///
/// <para>
/// Existe porque as três implementações divergiam, e nenhuma batia com a realidade:
/// </para>
/// <list type="bullet">
///   <item><description>
///     o <c>PollForQrCodeAsync</c> tratava apenas <c>Failed</c> como terminal — <c>Canceled</c> e
///     <c>RequestedCancel</c> gastavam as 10 tentativas inteiras à toa;
///   </description></item>
///   <item><description>
///     o XML doc de <c>Bs2CollectionOrderTransaction</c> já declarava <c>RequestedCancel</c> e
///     <c>Canceled</c> como terminais — documentação e código discordavam no mesmo arquivo;
///   </description></item>
///   <item><description>
///     o <c>Bs2PixAdapter</c> do <c>cambio-real-v3</c> listava 20 status, nenhum deles
///     <c>Issued</c> nem <c>QrCodeGenerated</c> — exatamente os dois que a BS2 mais usa.
///   </description></item>
/// </list>
///
/// <para>
/// <b>Fonte:</b> confirmado ao vivo contra a homologação em 2026-09-16 (<c>Issued</c> na criação,
/// <c>Failed</c> com <c>statusInformation: "QrCode generation failed."</c>) e no legado
/// (<c>PixService::getStatusPago</c>, <c>PayinNotification::check</c>).
/// </para>
///
/// <para>
/// <b>Regra de ouro:</b> status desconhecido é tratado como EM VOO, nunca como terminal. Encerrar
/// cedo por status não reconhecido descarta uma ordem que ainda podia ser paga; continuar pollando
/// custa, no pior caso, algumas tentativas. O erro assimétrico manda ficar do lado seguro.
/// </para>
/// </summary>
public static class Bs2CollectionOrderStatus
{
    /// <summary>Ordem criada; QR ainda não emitido. Observado ao vivo em 2026-09-16.</summary>
    public const string Issued = "Issued";

    /// <summary>QR Code gerado e apto a receber pagamento — NÃO significa pago.</summary>
    public const string QrCodeGenerated = "QrCodeGenerated";

    /// <summary>Pagamento liquidado.</summary>
    public const string Succeed = "Succeed";

    /// <summary>Falha definitiva. Observado ao vivo com <c>"QrCode generation failed."</c>.</summary>
    public const string Failed = "Failed";

    /// <summary>Cancelamento solicitado.</summary>
    public const string RequestedCancel = "RequestedCancel";

    /// <summary>Cancelada.</summary>
    public const string Canceled = "Canceled";

    /// <summary>
    /// Terminais: a ordem não será paga. Manter ESTREITO — só rejeições inequívocas.
    /// <c>Cancelled</c> (grafia britânica) incluída por defesa: não foi observada, mas o custo de
    /// aceitá-la é nulo e o de não aceitá-la seria polling inútil.
    /// </summary>
    public static readonly IReadOnlySet<string> Terminal =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Failed, RequestedCancel, Canceled, "Cancelled",
        };

    /// <summary>
    /// Em voo: caminho assíncrono normal, seguir pollando. Contém apenas o que a BS2 realmente
    /// envia — não inventar nomes plausíveis: status desconhecido já cai no lado seguro por
    /// <see cref="IsTerminal"/>, então engordar esta lista não compra nada e dá falsa confiança
    /// de que o vocabulário foi confirmado.
    /// </summary>
    public static readonly IReadOnlySet<string> Pending =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Issued, QrCodeGenerated,
        };

    /// <summary>Pago — terminal de sucesso.</summary>
    public static readonly IReadOnlySet<string> Paid =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Succeed };

    /// <summary>
    /// <see langword="true"/> só para rejeição inequívoca. Nulo, vazio ou desconhecido devolve
    /// <see langword="false"/> — ver "regra de ouro" na doc do tipo.
    /// </summary>
    public static bool IsTerminal(string? status) =>
        !string.IsNullOrWhiteSpace(status) && Terminal.Contains(status);

    /// <summary><see langword="true"/> se o pagamento foi liquidado.</summary>
    public static bool IsPaid(string? status) =>
        !string.IsNullOrWhiteSpace(status) && Paid.Contains(status);

    /// <summary>
    /// <see langword="true"/> se a ordem segue em voo — inclui o status DESCONHECIDO, por desenho.
    /// </summary>
    public static bool IsPending(string? status) => !IsTerminal(status) && !IsPaid(status);
}
