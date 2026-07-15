using CambioReal.Bs2.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CambioReal.Bs2;

/// <summary>Registro do cliente BS2 no container.</summary>
public static class Bs2ServiceCollectionExtensions
{
    /// <summary>
    /// Registra o cliente a partir de uma seção de configuração.
    /// </summary>
    /// <remarks>
    /// As credenciais precisam chegar por um provider seguro (variáveis de ambiente, user-secrets,
    /// Vault). Nunca versione <c>ClientId</c>/<c>ClientSecret</c> em <c>appsettings.json</c> — a
    /// fonte da verdade é o <c>pass</c>, <c>cambio-real-v2/providers/bs2/sandbox-env</c>.
    /// </remarks>
    public static IServiceCollection AddBs2Client(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services.AddBs2Client(configuration.Bind);
    }

    /// <summary>
    /// Registra <see cref="Bs2Client"/>, o provedor de token e os dois pipelines HTTP autenticados
    /// (um por escopo — <see cref="Bs2Scope.CollectionOrder"/> e <see cref="Bs2Scope.PaymentOrder"/>).
    /// </summary>
    public static IServiceCollection AddBs2Client(this IServiceCollection services, Action<Bs2Options> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddOptions<Bs2Options>().Validate(
            options =>
            {
                options.Validate();
                return true;
            },
            "A configuração do Bs2Options é inválida.");

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IBs2TokenProvider, Bs2TokenProvider>();

        // Cliente exclusivo do POST auth/oauth/v2/token: sem handler de autenticação, para não recorrer.
        services.AddHttpClient(Bs2ClientNames.Auth, ConfigureTransport);

        services.AddHttpClient(Bs2ClientNames.CollectionOrders, ConfigureTransport)
            .AddHttpMessageHandler(provider =>
                new Bs2AuthenticationHandler(provider.GetRequiredService<IBs2TokenProvider>(), Bs2Scope.CollectionOrder));

        services.AddHttpClient(Bs2ClientNames.PaymentOrders, ConfigureTransport)
            .AddHttpMessageHandler(provider =>
                new Bs2AuthenticationHandler(provider.GetRequiredService<IBs2TokenProvider>(), Bs2Scope.PaymentOrder));

        services.TryAddTransient(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            return new Bs2Client(
                factory.CreateClient(Bs2ClientNames.CollectionOrders),
                factory.CreateClient(Bs2ClientNames.PaymentOrders));
        });

        return services;
    }

    private static void ConfigureTransport(IServiceProvider provider, HttpClient client)
    {
        var options = provider.GetRequiredService<IOptions<Bs2Options>>().Value;
        options.Validate();

        client.BaseAddress = options.ResolveBaseAddress();
        client.Timeout = options.Timeout;
    }
}
