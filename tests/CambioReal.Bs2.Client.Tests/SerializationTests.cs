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
            {"currentPage":2,"pageSize":10,"totalRecords":25,"totalPages":3,"itens":[{"id":"a"},{"id":"b"}],"previousPage":1,"nextPage":3}
            """;

        var page = JsonSerializer.Deserialize<Bs2PagedResult<CollectionOrderDetails>>(json, Bs2Json.Options)!;

        page.CurrentPage.ShouldBe(2);
        page.Itens.Count.ShouldBe(2);
        page.Itens[0].Id.ShouldBe("a");
        page.NextPage.ShouldBe(3);
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
}
