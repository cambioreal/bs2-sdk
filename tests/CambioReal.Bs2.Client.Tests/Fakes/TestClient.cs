using System.Net;
using CambioReal.Bs2.Auth;

namespace CambioReal.Bs2.Tests.Fakes;

internal static class TestClient
{
    public static Bs2Options NewOptions() => new()
    {
        Environment = Bs2Environment.Sandbox,
        ClientId = "client-1",
        ClientSecret = "secret-1",
    };

    /// <summary>
    /// Monta um <see cref="Bs2Client"/> sobre um transporte gravado compartilhado pelos dois
    /// clientes internos (collection-orders/payment-orders), com token fixo. Compartilhar o
    /// transporte é seguro para os testes deste SDK: eles exercitam um recurso por vez, então a
    /// fila FIFO de respostas nunca precisa distinguir de qual escopo a requisição veio.
    /// </summary>
    public static (Bs2Client Client, RecordingHttpMessageHandler Transport) Create(
        params (HttpStatusCode Status, string Json)[] responses)
    {
        var transport = new RecordingHttpMessageHandler();

        foreach (var (status, json) in responses)
        {
            transport.RespondWith(status, json);
        }

        var options = NewOptions();

        var collectionOrdersHandler = new Bs2AuthenticationHandler(new StubTokenProvider("tok-1"), Bs2Scope.CollectionOrder)
        {
            InnerHandler = transport,
        };

        var paymentOrdersHandler = new Bs2AuthenticationHandler(new StubTokenProvider("tok-1"), Bs2Scope.PaymentOrder)
        {
            InnerHandler = transport,
        };

        var collectionOrdersClient = new HttpClient(collectionOrdersHandler) { BaseAddress = options.ResolveBaseAddress() };
        var paymentOrdersClient = new HttpClient(paymentOrdersHandler) { BaseAddress = options.ResolveBaseAddress() };

        var client = new Bs2Client(collectionOrdersClient, paymentOrdersClient);
        return (client, transport);
    }

    public static (Bs2Client Client, RecordingHttpMessageHandler Transport) CreateOk(string json = "{}") =>
        Create((HttpStatusCode.OK, json));
}
