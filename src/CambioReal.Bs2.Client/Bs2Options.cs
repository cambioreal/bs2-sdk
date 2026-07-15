namespace CambioReal.Bs2;

/// <summary>Configuração do <see cref="Bs2Client"/>.</summary>
public sealed class Bs2Options
{
    /// <summary>Nome da seção de configuração sugerida.</summary>
    public const string SectionName = "Bs2";

    /// <summary>Client ID OAuth2 (client_credentials), fornecido pela BS2.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client secret OAuth2. Deve vir do <c>pass</c> ou de um secret store — nunca do código.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Ambiente alvo. O padrão é <see cref="Bs2Environment.Sandbox"/>, deliberadamente.</summary>
    public Bs2Environment Environment { get; set; } = Bs2Environment.Sandbox;

    /// <summary>Sobrescreve o endereço base derivado de <see cref="Environment"/>. Precisa terminar em <c>/</c>.</summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// Margem de segurança para renovar o token antes do vencimento real. A BS2 não documenta
    /// refresh token — expirar significa reautenticar do zero via <c>POST auth/oauth/v2/token</c>.
    /// </summary>
    public TimeSpan TokenExpirationSkew { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Timeout de cada requisição HTTP.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Número de tentativas ao aguardar o QR code de uma ordem de cobrança ficar disponível
    /// (<see cref="Resources.CollectionOrdersResource.PollForQrCodeAsync"/>). Confirmado hardcoded
    /// em 10 no legado (<c>PixService::createRaw</c>) — aqui configurável, com o mesmo default.
    /// </summary>
    public int CollectionOrderPollTries { get; set; } = 10;

    /// <summary>Intervalo entre tentativas de polling do QR code. Confirmado em 1s no legado.</summary>
    public TimeSpan CollectionOrderPollDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Endereço base efetivo.</summary>
    public Uri ResolveBaseAddress() => BaseAddress ?? Environment.GetBaseAddress();

    /// <summary>Valida a configuração e lança se estiver inconsistente.</summary>
    /// <exception cref="InvalidOperationException">Alguma credencial obrigatória está ausente ou o base address é inválido.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new InvalidOperationException($"{nameof(Bs2Options)}.{nameof(ClientId)} é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(ClientSecret))
        {
            throw new InvalidOperationException($"{nameof(Bs2Options)}.{nameof(ClientSecret)} é obrigatório.");
        }

        var baseAddress = ResolveBaseAddress();

        if (!baseAddress.IsAbsoluteUri)
        {
            throw new InvalidOperationException($"{nameof(BaseAddress)} precisa ser absoluto.");
        }

        if (!baseAddress.AbsolutePath.EndsWith('/'))
        {
            throw new InvalidOperationException($"{nameof(BaseAddress)} precisa terminar em '/' (recebido: '{baseAddress}').");
        }

        if (TokenExpirationSkew < TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{nameof(TokenExpirationSkew)} não pode ser negativo.");
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{nameof(Timeout)} precisa ser positivo.");
        }

        if (CollectionOrderPollTries < 1)
        {
            throw new InvalidOperationException($"{nameof(CollectionOrderPollTries)} precisa ser >= 1.");
        }

        if (CollectionOrderPollDelay < TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{nameof(CollectionOrderPollDelay)} não pode ser negativo.");
        }
    }
}
