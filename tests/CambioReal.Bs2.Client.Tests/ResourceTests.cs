using System.Net;
using CambioReal.Bs2.Http;
using CambioReal.Bs2.Models;
using CambioReal.Bs2.Tests.Fakes;
using Shouldly;
using Xunit;

namespace CambioReal.Bs2.Tests;

public sealed class ResourceTests
{
    private static readonly Bs2CdeParty Debtor = new()
    {
        FinancialInstitution = "71027866",
        Issuer = "0001",
        Account = "9054758-6",
        AccountType = "Current",
        IbanCode = "BR0971027866000010090547586C1",
        Identification = "12345678900",
        IdentificationType = "CPF",
        Name = "Fulano de Tal",
    };

    [Fact]
    public async Task CollectionOrders_CreateAsync_PostsExactPayloadAndReturnsBareStringId()
    {
        var (client, transport) = TestClient.Create((HttpStatusCode.OK, "\"order-123\""));

        var request = new CreateCollectionOrderRequest
        {
            Amount = 100.00m,
            ExternalId = "ext-1",
            Information = "CambioReal ext-1",
            CreditorDebtorType = "01",
            Debtor = Debtor,
            ForeignCreditor = new Bs2ForeignParty("US", "Jane Doe"),
        };

        var orderId = await client.CollectionOrders.CreateAsync(request);

        orderId.ShouldBe("order-123");

        var recorded = transport.Requests.Single();
        recorded.Method.ShouldBe(HttpMethod.Post);
        recorded.RequestUri!.AbsolutePath.ShouldBe("/core2/pix/cambio/v1/collection-orders");
        recorded.Body!.ShouldContain("\"externalId\":\"ext-1\"");
        recorded.Body!.ShouldContain("\"transactionReason\":1");
        recorded.Body!.ShouldContain("\"creditorDebtorType\":\"01\"");
        recorded.Body!.ShouldContain("\"financialInstitution\":\"71027866\"");
        recorded.Body!.ShouldContain("\"cde\":true");
        recorded.Body!.ShouldContain("\"identificationType\":\"CPF\"");
        recorded.Body!.ShouldContain("\"foreignCreditor\":");
    }

    [Fact]
    public async Task CollectionOrders_CreateAsync_SendsIdempotencyKeyHeaderWhenProvided()
    {
        var (client, transport) = TestClient.Create((HttpStatusCode.OK, "\"order-123\""));

        var request = new CreateCollectionOrderRequest
        {
            Amount = 100.00m,
            ExternalId = "ext-1",
            Information = "CambioReal ext-1",
            CreditorDebtorType = "01",
            Debtor = Debtor,
            ForeignCreditor = new Bs2ForeignParty("US", "Jane Doe"),
        };

        await client.CollectionOrders.CreateAsync(request, idempotencyKey: "bs2:payin:ext-1");

        transport.Requests.Single().IdempotencyKey.ShouldBe("bs2:payin:ext-1");
    }

    [Fact]
    public async Task CollectionOrders_CreateAsync_OmitsIdempotencyKeyHeaderWhenNotProvided()
    {
        var (client, transport) = TestClient.Create((HttpStatusCode.OK, "\"order-123\""));

        var request = new CreateCollectionOrderRequest
        {
            Amount = 100.00m,
            ExternalId = "ext-1",
            Information = "CambioReal ext-1",
            CreditorDebtorType = "01",
            Debtor = Debtor,
            ForeignCreditor = new Bs2ForeignParty("US", "Jane Doe"),
        };

        await client.CollectionOrders.CreateAsync(request);

        transport.Requests.Single().IdempotencyKey.ShouldBeNull();
    }

    /// <summary>
    /// Exercita o pipeline completo (não só <c>TokenProviderTests</c>): um 401 dispara exatamente
    /// uma reautenticação/retentativa via <c>Bs2AuthenticationHandler</c>, e a segunda tentativa
    /// bem-sucedida é o resultado devolvido ao chamador — regra canônica do goal-loop
    /// ("no máximo um retry em 401 quando aplicável").
    /// </summary>
    [Fact]
    public async Task CollectionOrders_GetAsync_RetriesOnceAfter401ThenSucceeds()
    {
        var (client, transport) = TestClient.Create(
            (HttpStatusCode.Unauthorized, "{}"),
            (HttpStatusCode.OK, """{"id":"order-123","transaction":{"status":"Succeed"}}"""));

        var details = await client.CollectionOrders.GetAsync("order-123");

        details.Transaction!.Status.ShouldBe("Succeed");
        transport.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CollectionOrders_GetAsync_ParsesNestedQrCodeAndStatus()
    {
        const string json = """
            {
              "id": "order-123",
              "externalId": "ext-1",
              "transaction": { "status": "QrCodeGenerated", "qrCode": "000201...6304ABCD", "amount": 100.00 }
            }
            """;

        var (client, transport) = TestClient.CreateOk(json);

        var details = await client.CollectionOrders.GetAsync("order-123");

        details.Transaction!.Status.ShouldBe("QrCodeGenerated");
        details.Transaction!.QrCode.ShouldBe("000201...6304ABCD");

        var recorded = transport.Requests.Single();
        recorded.Method.ShouldBe(HttpMethod.Get);
        recorded.RequestUri!.AbsolutePath.ShouldBe("/core2/pix/cambio/v1/collection-orders/order-123");
    }

    [Fact]
    public async Task CollectionOrders_ListAsync_BuildsQueryAndParsesItensKey()
    {
        const string json = """
            {"currentPage":1,"pageSize":20,"totalRecords":1,"totalPages":1,"itens":[{"id":"order-123"}]}
            """;

        var (client, transport) = TestClient.CreateOk(json);

        var page = await client.CollectionOrders.ListAsync(new DateOnly(2026, 7, 15));

        page.Itens.Count.ShouldBe(1);
        page.Itens[0].Id.ShouldBe("order-123");

        var recorded = transport.Requests.Single();
        recorded.RequestUri!.PathAndQuery.ShouldBe(
            "/core2/pix/cambio/v1/collection-orders?DateUtc=2026-07-15&CurrentPage=1&QuantityPerPage=20");
    }

    [Fact]
    public async Task CollectionOrders_CancelAsync_SendsDeleteToOrderPath()
    {
        var (client, transport) = TestClient.CreateOk();

        await client.CollectionOrders.CancelAsync("order-123");

        var recorded = transport.Requests.Single();
        recorded.Method.ShouldBe(HttpMethod.Delete);
        recorded.RequestUri!.AbsolutePath.ShouldBe("/core2/pix/cambio/v1/collection-orders/order-123");
    }

    [Fact]
    public async Task CollectionOrders_PollForQrCodeAsync_StopsAsSoonAsQrCodeAppears()
    {
        var (client, transport) = TestClient.Create(
            (HttpStatusCode.OK, """{"id":"order-123","transaction":{"status":"Issued"}}"""),
            (HttpStatusCode.OK, """{"id":"order-123","transaction":{"status":"QrCodeGenerated","qrCode":"abc"}}"""));

        var details = await client.CollectionOrders.PollForQrCodeAsync("order-123", maxTries: 10, delay: TimeSpan.Zero);

        details.Transaction!.QrCode.ShouldBe("abc");
        transport.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CollectionOrders_PollForQrCodeAsync_StopsOnFailedStatus()
    {
        var (client, transport) = TestClient.Create(
            (HttpStatusCode.OK, """{"id":"order-123","transaction":{"status":"Failed"}}"""));

        var details = await client.CollectionOrders.PollForQrCodeAsync("order-123", maxTries: 10, delay: TimeSpan.Zero);

        details.Transaction!.Status.ShouldBe("Failed");
        transport.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task PaymentOrders_CreateByPixKeyAsync_PostsThinCreditorAndSiblingCreditorDict()
    {
        var (client, transport) = TestClient.Create((HttpStatusCode.OK, "\"payout-1\""));

        var request = new CreatePaymentOrderByPixKeyRequest
        {
            Amount = 50.00m,
            ExternalId = "ext-2",
            Information = "CambioReal ext-2",
            CreditorDebtorType = "01",
            Creditor = new Bs2DictKeyCreditor { IbanCode = "BR0971027866000010090547586C1" },
            CreditorDict = new Bs2CreditorDict("+5511999998888", "PHONE"),
            ForeignDebtor = new Bs2ForeignParty("US", "Jane Doe"),
        };

        var orderId = await client.PaymentOrders.CreateByPixKeyAsync(request);

        orderId.ShouldBe("payout-1");

        var recorded = transport.Requests.Single();
        recorded.RequestUri!.AbsolutePath.ShouldBe("/core2/pix/cambio/v1/payment-orders/dict-key");
        // O encoder padrão de System.Text.Json (JsonSerializerDefaults.Web) escapa o sinal de
        // adição da chave PIX em telefone como sequência unicode — JSON válido, semanticamente
        // idêntico, mas não aparece literal no corpo serializado.
        recorded.Body!.ShouldContain("\"creditor\":{\"cde\":true,\"ibanCode\":\"BR0971027866000010090547586C1\"}");
        recorded.Body!.ShouldContain("\"creditorDict\":{\"dictKey\":\"\\u002B5511999998888\",\"dictKeyType\":\"PHONE\"}");
        recorded.Body!.ShouldNotContain("financialInstitution");
    }

    [Fact]
    public async Task PaymentOrders_CreateByAccountAsync_UsesFinancialInstitutionIssuerAccountNotBankCodeFields()
    {
        var (client, transport) = TestClient.Create((HttpStatusCode.OK, "\"payout-2\""));

        var request = new CreatePaymentOrderByAccountRequest
        {
            Amount = 75.00m,
            ExternalId = "ext-3",
            Information = "CambioReal ext-3",
            CreditorDebtorType = "01",
            Creditor = Debtor,
            ForeignDebtor = new Bs2ForeignParty("US", "Jane Doe"),
        };

        var orderId = await client.PaymentOrders.CreateByAccountAsync(request);

        orderId.ShouldBe("payout-2");

        var recorded = transport.Requests.Single();
        recorded.RequestUri!.AbsolutePath.ShouldBe("/core2/pix/cambio/v1/payment-orders/account-data");

        // Correção confirmada vs. a suposição do adapter C# greenfield (bankCode/accountNumber/
        // routingNumber/bicCode) — os nomes reais são estes:
        recorded.Body!.ShouldContain("\"financialInstitution\":\"71027866\"");
        recorded.Body!.ShouldContain("\"issuer\":\"0001\"");
        recorded.Body!.ShouldContain("\"account\":\"9054758-6\"");
        recorded.Body!.ShouldNotContain("bankCode");
        recorded.Body!.ShouldNotContain("routingNumber");
        recorded.Body!.ShouldNotContain("bicCode");
    }

    [Fact]
    public async Task PaymentOrders_RefundAsync_UsesTheSameAccountDataPathAsCreateByAccount()
    {
        var (client, transport) = TestClient.Create((HttpStatusCode.OK, "\"refund-1\""));

        var request = new CreatePaymentOrderByAccountRequest
        {
            Amount = 20.00m,
            ExternalId = "ext-4",
            Information = "CambioReal ext-4 refund",
            CreditorDebtorType = "01",
            Creditor = Debtor,
            ForeignDebtor = new Bs2ForeignParty("US", "Jane Doe"),
        };

        await client.PaymentOrders.RefundAsync(request);

        transport.Requests.Single().RequestUri!.AbsolutePath.ShouldBe("/core2/pix/cambio/v1/payment-orders/account-data");
    }

    [Fact]
    public async Task PaymentOrders_GetAsync_QueriesPaymentOrderByIdAndMapsStatus()
    {
        const string json = """{"id":"payout-2","transaction":{"status":"Succeed","settlementType":"PixByAccountData"}}""";
        var (client, transport) = TestClient.CreateOk(json);

        var details = await client.PaymentOrders.GetAsync("payout-2");

        details.Transaction!.Status.ShouldBe("Succeed");
        transport.Requests.Single().RequestUri!.AbsolutePath.ShouldBe("/core2/pix/cambio/v1/payment-orders/payout-2");
    }

    [Fact]
    public async Task PaymentOrders_ListAsync_BuildsQueryAndParsesItensKey()
    {
        const string json = """{"currentPage":1,"pageSize":20,"totalRecords":0,"totalPages":0,"itens":[]}""";
        var (client, transport) = TestClient.CreateOk(json);

        var page = await client.PaymentOrders.ListAsync(new DateOnly(2026, 7, 15));

        page.Itens.Count.ShouldBe(0);
        transport.Requests.Single().RequestUri!.PathAndQuery.ShouldBe(
            "/core2/pix/cambio/v1/payment-orders?DateUtc=2026-07-15&CurrentPage=1&QuantityPerPage=20");
    }

    [Fact]
    public async Task NonSuccessResponse_MessageObjectShape_ExtractsMessage()
    {
        var (client, _) = TestClient.Create((HttpStatusCode.BadRequest, """{"message":"Client not located in context"}"""));

        var error = await Should.ThrowAsync<Bs2ApiException>(
            async () => await client.CollectionOrders.GetAsync("order-123"));

        error.ErrorCode.ShouldBe("Client not located in context");
        error.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>Shape de fixture confirmado em <c>config/bs2-mock.php</c> (legado).</summary>
    [Fact]
    public async Task NonSuccessResponse_TagDescricaoArrayShape_CombinesBoth()
    {
        var (client, _) = TestClient.Create((HttpStatusCode.BadRequest, """[{"tag":"CT02098","descricao":"Client not located in context"}]"""));

        var error = await Should.ThrowAsync<Bs2ApiException>(
            async () => await client.CollectionOrders.GetAsync("order-123"));

        error.ErrorCode.ShouldBe("CT02098: Client not located in context");
    }

    [Fact]
    public async Task NonSuccessResponse_DescriptionOnlyArrayShape_ExtractsDescription()
    {
        var (client, _) = TestClient.Create((HttpStatusCode.BadRequest, """[{"description":"Invalid amount"}]"""));

        var error = await Should.ThrowAsync<Bs2ApiException>(
            async () => await client.CollectionOrders.GetAsync("order-123"));

        error.ErrorCode.ShouldBe("Invalid amount");
    }
}
