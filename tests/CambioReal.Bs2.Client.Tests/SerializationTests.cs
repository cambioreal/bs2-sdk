using System.Text.Json;
using CambioReal.Bs2.Models;
using CambioReal.Bs2.Serialization;
using Shouldly;
using Xunit;

namespace CambioReal.Bs2.Tests;

public sealed class SerializationTests
{
    [Fact]
    public void CreateCollectionOrderRequestSerializesCamelCaseFieldsAndDefaults()
    {
        var request = new CreateCollectionOrderRequest
        {
            Amount = 100.00m,
            ExternalId = "ext-1",
            Information = "CambioReal ext-1",
            CreditorDebtorType = "01",
            Debtor = new Bs2CdeParty
            {
                FinancialInstitution = "71027866",
                Issuer = "0001",
                Account = "9054758-6",
                AccountType = "Current",
                IbanCode = "BR0971027866000010090547586C1",
                Identification = "12345678900",
                IdentificationType = "CPF",
                Name = "Fulano de Tal",
            },
            ForeignCreditor = new Bs2ForeignParty("US", "Jane Doe"),
        };

        var json = JsonSerializer.Serialize(request, Bs2Json.Options);

        json.ShouldContain("\"amount\":100.00");
        json.ShouldContain("\"externalId\":\"ext-1\"");
        json.ShouldContain("\"transactionReason\":1");
        json.ShouldContain("\"accountType\":\"Current\"");
        json.ShouldContain("\"identificationType\":\"CPF\"");
        json.ShouldContain("\"foreignCreditor\":{\"country\":\"US\",\"name\":\"Jane Doe\"}");
    }

    [Fact]
    public void Bs2PagedResultDeserializesItensKeyNotItems()
    {
        const string json = """
            {"currentPage":2,"pageSize":10,"totalRecords":25,"totalPages":3,"itens":[{"id":"a"},{"id":"b"}],"previousPage":true,"nextPage":true}
            """;

        var page = JsonSerializer.Deserialize<Bs2PagedResult<CollectionOrderDetails>>(json, Bs2Json.Options)!;

        page.CurrentPage.ShouldBe(2);
        page.Itens.Count.ShouldBe(2);
        page.Itens[0].Id.ShouldBe("a");
        page.NextPage.ShouldBe(true);
        page.PreviousPage.ShouldBe(true);
    }

    [Fact]
    public void CollectionOrderDetailsDeserializesNestedTransactionQrCode()
    {
        const string json = """
            {
              "id": "order-123",
              "externalId": "ext-1",
              "createdDate": "2026-07-15T12:00:00Z",
              "transaction": {
                "paymentDate": "2026-07-15T12:05:00Z",
                "amount": 100.00,
                "paymentType": "DebtorCDE",
                "status": "QrCodeGenerated",
                "endToEndId": "E00000000202607151200abc",
                "qrCode": "000201...6304ABCD"
              },
              "foreignCreditor": { "country": "US", "name": "Jane Doe" }
            }
            """;

        var details = JsonSerializer.Deserialize<CollectionOrderDetails>(json, Bs2Json.Options)!;

        details.Id.ShouldBe("order-123");
        details.Transaction!.Status.ShouldBe("QrCodeGenerated");
        details.Transaction!.QrCode.ShouldBe("000201...6304ABCD");
        details.Transaction!.Amount.ShouldBe(100.00m);
        details.ForeignCreditor!.Country.ShouldBe("US");
    }

    [Fact]
    public void BareStringResponseDeserializesAsPlainString()
    {
        const string json = "\"order-123\"";

        var orderId = JsonSerializer.Deserialize<string>(json, Bs2Json.Options);

        orderId.ShouldBe("order-123");
    }

    [Fact]
    public void CreatePaymentOrderByPixKeyRequestKeepsCreditorAndCreditorDictAsSiblings()
    {
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

        var json = JsonSerializer.Serialize(request, Bs2Json.Options);

        // O encoder padrão de System.Text.Json (JsonSerializerDefaults.Web) escapa o sinal de
        // adição da chave PIX em telefone como sequência unicode — JSON válido, semanticamente
        // idêntico, mas não aparece literal no texto.
        json.ShouldContain("\"creditor\":{\"cde\":true,\"ibanCode\":\"BR0971027866000010090547586C1\"}");
        json.ShouldContain("\"creditorDict\":{\"dictKey\":\"\\u002B5511999998888\",\"dictKeyType\":\"PHONE\"}");
    }

    /// <summary>
    /// Regressao: a listagem devolve um `debtor` ENXUTO (so `account`/`identification`), enquanto
    /// o detalhe devolve o objeto completo. Enquanto `CollectionOrderDetails.Debtor` era
    /// `Bs2CdeParty` (com membros `required`), este payload lancava
    /// `JsonException: missing required properties` e o gateway respondia 500.
    ///
    /// JSON abaixo e a resposta REAL da BS2 em 2026-09-16 (homologacao), nao um mock inventado.
    /// </summary>
    [Fact]
    public void CollectionOrderListDeserializesSlimDebtorFromRealBs2Payload()
    {
        const string json = """
            {"currentPage":1,"pageSize":10,"totalRecords":1,"totalPages":1,
             "itens":[{"id":"7d94d777-9df4-4912-8d87-d6cf69a22a07","externalId":"CRQR260916192021",
             "createdDate":"2026-09-16T19:20:23.9969203+00:00",
             "transaction":{"paymentDate":null,"amount":100.0,"paymentType":"DebtorCDE","status":"Failed","endToEndId":null},
             "debtor":{"account":"90547586","identification":"52998224725"},
             "foreignCreditor":{"country":"US"},
             "qrCodeType":1,"originalCollectionOrderId":null}],
             "previousPage":false,"nextPage":false}
            """;

        var page = JsonSerializer.Deserialize<Bs2PagedResult<CollectionOrderDetails>>(json, Bs2Json.Options)!;

        page.TotalRecords.ShouldBe(1);
        var item = page.Itens[0];
        item.ExternalId.ShouldBe("CRQR260916192021");

        // presentes na listagem
        item.Debtor.ShouldNotBeNull();
        item.Debtor!.Account.ShouldBe("90547586");
        item.Debtor.Identification.ShouldBe("52998224725");

        // ausentes na listagem — devem desserializar como null, nao lancar
        item.Debtor.FinancialInstitution.ShouldBeNull();
        item.Debtor.Issuer.ShouldBeNull();
        item.Debtor.AccountType.ShouldBeNull();
        item.Debtor.IbanCode.ShouldBeNull();
        item.Debtor.Name.ShouldBeNull();
    }

    /// <summary>
    /// O detalhe traz o objeto completo — o mesmo tipo de resposta tem de aceitar os dois shapes.
    /// Payload real da BS2 (2026-09-16), com `identificationType` capitalizado ("Cpf"), que e
    /// como a BS2 DEVOLVE (o request exige "CPF").
    /// </summary>
    [Fact]
    public void CollectionOrderDetailDeserializesFullDebtorFromRealBs2Payload()
    {
        const string json = """
            {"id":"7d94d777-9df4-4912-8d87-d6cf69a22a07","externalId":"CRQR260916192021",
             "transaction":{"paymentDate":null,"amount":100.0,"paymentType":"DebtorCDE","status":"Failed",
                            "statusInformation":"QrCode generation failed.","endToEndId":null,"qrCode":null},
             "debtor":{"financialInstitution":"71027866","issuer":"0001","account":"90547586",
                       "accountType":"Current","cde":true,"ibanCode":"BR0971027866000010090547586C1",
                       "identification":"52998224725","identificationType":"Cpf","name":"E2E Test User"},
             "foreignCreditor":{"name":"CambioReal Inc","country":"US"},"qrCodeType":1}
            """;

        var details = JsonSerializer.Deserialize<CollectionOrderDetails>(json, Bs2Json.Options)!;

        details.Debtor.ShouldNotBeNull();
        details.Debtor!.FinancialInstitution.ShouldBe("71027866");
        details.Debtor.IbanCode.ShouldBe("BR0971027866000010090547586C1");
        details.Debtor.IdentificationType.ShouldBe("Cpf");
        details.Debtor.Cde.ShouldBe(true);
        details.Transaction!.StatusInformation.ShouldBe("QrCode generation failed.");
    }
}
