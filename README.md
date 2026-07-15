# bs2-sdk

Cliente .NET tipado para a **BS2 (Banco Bonsucesso) PIX Câmbio API** (payin via
collection-orders, payout via payment-orders) — mesmo padrão arquitetural do
[`kira-sdk`](https://github.com/cambioreal/kira-sdk) e do
[`ripple-sdk`](https://github.com/cambioreal/ripple-sdk): pacote `CambioReal.Bs2.Client`,
transporte + auth (OAuth2 client_credentials, cache single-flight) + recursos tipados, target
`net10.0`.

## Por que este pacote existe

Parte do goal-loop "Providers standalone com sandbox real" (`GOAL-provider-standalone-sandbox-loop.md`):
evoluir cada provider de pagamento para um serviço standalone testável de ponta a ponta, um de
cada vez. BS2 é o primeiro da fila (PIX payin+payout, o provider com outbound mais completo no
framework legado). Mesmo padrão já provado por Kira/Ripple: SDK standalone + gateway HTTP
(`bs2-gateway`, a construir na próxima etapa), não embutido no monolito `cambio-real-v3`.

## O que está confirmado vs. inferido

Todo endpoint e formato de payload deste SDK foi **confirmado linha a linha contra o código
legado real** (`cerebro/app/Libraries/Bs2/{AbstractService,PixService,PayoutService,PayoutNotification}.php`),
não contra a documentação pública da BS2 nem contra o adapter C# greenfield não confirmado
(`cambio-real-v3/.../Bs2{Pix,Settlement}Adapter.cs`, que tem múltiplas suposições erradas —
ver "Correções" abaixo).

| Item | Status |
|---|---|
| Token (`POST auth/oauth/v2/token`, form-urlencoded, um token por escopo) | **✅ confirmado AO VIVO contra o sandbox** (2026-07-15, HTTP 200 real, ambos os escopos: `pix.cambio.collection.order` e `pix.cambio.payment.order`) |
| `GET collection-orders`/`GET payment-orders` (list, leitura) | 🔴 **bloqueado ao vivo por `403`** — client sandbox não provisionado para nenhum recurso `core2/pix/cambio/v1/*`, nem leitura (ver "Bloqueio de provisionamento" abaixo) |
| Criação de collection-order (payin) | 🟡 Modelado a partir do payload exato do legado — **não exercitado ao vivo** (bloqueado por autorização financeira pendente E pelo bloqueio de provisionamento) |
| Criação de payment-order (payout, dict-key/account-data) | 🟡 Idem |
| Webhooks (payin/payout) | 🟡 Modelado a partir do legado — HMAC-SHA256 (`X-BS2-Signature`) + IP allowlist; implementação do handler HTTP fica no `bs2-gateway`, não neste SDK |

Suíte de integração sandbox real, opt-in (nunca roda em CI por padrão):
[`tests/CambioReal.Bs2.Client.SandboxTests`](tests/CambioReal.Bs2.Client.SandboxTests/README.md) —
confirma auth ao vivo para os dois escopos e reporta o status real do bloqueio de provisionamento
(§3) via o SDK real, não `curl` manual.

## Bloqueio de provisionamento (P0, externo ao código)

O client `cambio-real-v2/providers/bs2/sandbox-env` emite tokens OAuth2 válidos para os dois
escopos, mas recebe `403` em **toda** chamada de recurso testada — inclusive `GET` de listagem,
não financeira. Isso é mais amplo que o gap já documentado em `provider-protocol/docs/PROVIDER-MAP.md`
(que só registrava `POST create` → `400 CT02098: Client not located in context`).

**Descartado "client mal configurado" (2026-07-15):** testados também os dois clients que o
legado `cerebro` efetivamente usa hoje — `pass cambio-real/bs2/cerebro-demo-env` e
`pass cambio-real/bs2/demo-env` — mesmo resultado: autenticam, `403` em `GET collection-orders`.
Três clients diferentes, mesmo ambiente sandbox (`apihmz.bancobonsucesso.com.br`), mesmo bloqueio.
Isso confirma que o problema é do ambiente/contexto de conta no lado BS2, não de uma credencial
específica mal provisionada. Nenhum teste de sandbox real além da autenticação pode avançar até a
BS2 provisionar contra um contexto de conta válido — ação externa, não resolvível por código. Ver
`docs/providers/bs2/discovery.md` §3 para o registro completo.

## Correções vs. o adapter C# greenfield (`cambio-real-v3`, não confirmado)

O adapter `Bs2SettlementAdapter.cs` do checkout `cambio-real-v3` assumia, sem confirmação ao
vivo, que o corpo do payout via conta bancária usava
`creditor.{name,bankCode,accountNumber,routingNumber,bicCode}`. Confirmado contra o legado
(`PayoutService::createPixByAccount`) que os nomes reais são:

```
creditor: { financialInstitution, issuer, account, accountType, cde, ibanCode, identification, identificationType, name }
```

Não existe `bicCode`. `ibanCode` é o código IBAN-like da **própria conta BS2 do cliente**
(config), não um SWIFT/BIC do beneficiário. Outras correções (resposta de create é string simples,
não objeto; chave de listagem é `itens`, não `items`; sem `Idempotency-Key` real no legado) estão
documentadas em `docs/providers/bs2/discovery.md`.

## Modelo de domínio

Fluxo confirmado no legado — payin (collection-order) e payout (payment-order) são operações
independentes de um passo só (não há fluxo quote→identity→transfer como na Ripple):

```csharp
services.AddBs2Client(options =>
{
    options.Environment = Bs2Environment.Sandbox;
    options.ClientId = "...";      // pass cambio-real-v2/providers/bs2/sandbox-env
    options.ClientSecret = "...";  // idem
});
```

```csharp
// Payin
var orderId = await client.CollectionOrders.CreateAsync(new CreateCollectionOrderRequest
{
    Amount = 100.00m,
    ExternalId = externalId,
    Information = $"CambioReal {externalId}",
    CreditorDebtorType = "01", // CPF
    Debtor = new Bs2CdeParty { FinancialInstitution = "...", Issuer = "...", Account = "...",
        AccountType = "Current", IbanCode = "...", Identification = "...", IdentificationType = "CPF", Name = "..." },
    ForeignCreditor = new Bs2ForeignParty("US", "Jane Doe"),
});

var details = await client.CollectionOrders.PollForQrCodeAsync(orderId, maxTries: 10, delay: TimeSpan.FromSeconds(1));

// Payout via chave PIX
var payoutId = await client.PaymentOrders.CreateByPixKeyAsync(new CreatePaymentOrderByPixKeyRequest
{
    Amount = 50.00m,
    ExternalId = externalId,
    Information = $"CambioReal {externalId}",
    CreditorDebtorType = "01",
    Creditor = new Bs2DictKeyCreditor { IbanCode = "..." },
    CreditorDict = new Bs2CreditorDict(pixKey, "PHONE"),
    ForeignDebtor = new Bs2ForeignParty("US", "Jane Doe"),
});
```

## Webhook — nunca confiar no status do corpo

O vocabulário de status do payload de webhook diverge entre as duas implementações do legado
(inglês vs. português) e nenhuma foi confirmada ao vivo. O padrão canônico deste SDK (mesmo do
legado battle-tested `PayinNotification`/`PayoutNotification`) é: **o webhook é só um gatilho**;
o handler HTTP (no `bs2-gateway`) deve sempre re-consultar `CollectionOrders.GetAsync`/
`PaymentOrders.GetAsync` (ou `CollectionOrders.PollForQrCodeAsync`) para obter o status real, nunca
confiar no corpo do webhook em si.

## Secrets

Credenciais de sandbox em `pass cambio-real-v2/providers/bs2/sandbox-env`; webhook HMAC secret em
`pass cambio-real-v2/providers/bs2-webhook-secret` — nunca hardcoded, nunca versionadas em
`appsettings.json`.

## Origem

Discovery completo, matriz de cobertura de endpoints e ADR curto em
[`docs/providers/bs2/discovery.md`](docs/providers/bs2/discovery.md). Contrato canônico de
plataforma em [`cambioreal/provider-protocol`](https://github.com/cambioreal/provider-protocol)
(perfil `Sync`).
