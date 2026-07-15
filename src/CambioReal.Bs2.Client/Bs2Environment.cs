namespace CambioReal.Bs2;

/// <summary>Ambiente da API BS2 PIX Câmbio.</summary>
public enum Bs2Environment
{
    /// <summary>Sandbox/homolog — <c>https://apihmz.bancobonsucesso.com.br/</c>.</summary>
    Sandbox = 0,

    /// <summary>Produção — <c>https://api.bs2.com/</c>.</summary>
    Production = 1,
}

/// <summary>Resolve o endereço base de cada <see cref="Bs2Environment"/>.</summary>
public static class Bs2EnvironmentExtensions
{
    /// <summary>
    /// Endereço base do ambiente, confirmado em <c>cerebro/config/bs2.php</c>
    /// (<c>connections.demo.url</c> / <c>connections.production.url</c>).
    /// </summary>
    public static Uri GetBaseAddress(this Bs2Environment environment) => environment switch
    {
        Bs2Environment.Production => new Uri("https://api.bs2.com/", UriKind.Absolute),
        Bs2Environment.Sandbox => new Uri("https://apihmz.bancobonsucesso.com.br/", UriKind.Absolute),
        _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, "Ambiente BS2 desconhecido."),
    };
}
