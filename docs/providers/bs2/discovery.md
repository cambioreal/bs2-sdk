# BS2 (Banco Bonsucesso) — Discovery

Status: SDK (`bs2-sdk`, público, `CambioReal.Bs2.Client` 0.1.0 no GitHub Packages) e gateway
(`bs2-gateway`, privado, imagem `ghcr.io/cambioreal/bs2-gateway:sha-b36a8670`) implementados,
publicados, **deployados e verificados ao vivo no k3s `.70`** (2026-07-15, autorizado
explicitamente pelo dono) — ver `bs2-sdk/README.md` e `bs2-gateway/README.md` para o status
detalhado de cada. GitOps: `HideakiSolutions/platform-gitops#363` mergeado, `Application`/
`AppProject` bootstrapados, pod `1/1 Running`, health checks `200`, chamada real end-to-end
(`GET /v1/bs2/payins`) confirma `403 BS2_PROVIDER_FORBIDDEN` corretamente classificado — todo o
pipeline funciona. Bloqueio de provisionamento BS2 (§3) segue de pé (externo, não resolvível por
código) — confirmado com dois clients BS2 distintos, incluindo o que o legado `cerebro`
efetivamente usa hoje, e agora também reconfirmado a partir do serviço real deployado, não só do
SDK local.
Provider order position: **1 of 9** (`GOAL-provider-standalone-sandbox-loop.md`).
Verified: 2026-07-15, against `cambio-real-v2/providers/bs2/sandbox-env` live sandbox + legacy
`cerebro` (read-only) + greenfield `cambio-real-v3` adapters (read-only, unverified-assumption tier).

## 1. Perfil no Provider Protocol

**`Sync`** (submit/status/cancel/refund), perfil PIX payin + payout puro. Confirmado contra
`provider-protocol/docs/PROVIDER-MAP.md` §1 ("BS2 | PIX payin+payout") e `RFC-provider-protocol.md`.//
Não há fluxo quote→identity→transfer (não é `Async`), não há custódia/KYC embutido. BS2 não
implementará `ISyncProviderAdapter` formalmente nesta wave — mesma decisão já tomada para
Kira/Ripple (SDK modela a API nativa do provider; o gateway traduz para `Envelope<T>`). Ver
`RFC-provider-protocol.md` §6 "Adoção futura" para a lacuna conhecida.

## 2. Ambiente e conectividade

| | Sandbox/homolog | Produção |
|---|---|---|
| Base URL | `https://apihmz.bancobonsucesso.com.br/` | `https://api.bs2.com/` |
| Client ID | `cambio-real-v2/providers/bs2/sandbox-env` (`CLIENT_ID`) | não aprovisionada neste loop |
| Client Secret | idem (`CLIENT_SECRET`) | — |
| mTLS / IP allowlist | Nenhum mTLS. IP allowlist existe apenas no lado do **webhook inbound** (BS2→nós), não no outbound (nós→BS2). | — |
| Webhook secret | `cambio-real-v2/providers/bs2-webhook-secret` (HMAC-SHA256) | — |

Nenhuma credencial foi impressa, logada ou copiada para este arquivo — apenas os nomes das
entradas no `pass`.

## 3. Auth — OAuth2 client_credentials

- `POST auth/oauth/v2/token`, `Content-Type: application/x-www-form-urlencoded`.
- Corpo: `grant_type=client_credentials&client_id=...&client_secret=...&scope=...`. Credenciais
  **sempre no corpo**, nunca em `Authorization: Basic`.
- **Um scope por token** — não há token combinado multi-scope:
  - `pix.cambio.collection.order` para payin.
  - `pix.cambio.payment.order` para payout.
- Resposta: `{ access_token, token_type: "Bearer", expires_in, scope }`.
- Aplicado como `Authorization: Bearer <access_token>` padrão.
- Sem retry automático em falha de auth no legado — se `authenticate()` falha, a chamada aborta.
  O SDK novo deve implementar single-flight + cache por escopo (2 caches independentes, um por
  scope) e no máximo 1 retry em 401 pós-obtenção de token (consistente com o padrão canônico do
  goal, não com o legado que não faz retry nenhum).

**Validado ao vivo 2026-07-15** (probe não financeiro, sandbox real):

| Probe | Resultado |
|---|---|
| `POST auth/oauth/v2/token` scope `pix.cambio.collection.order` | ✅ `200`, `token_type=Bearer`, `expires_in=1680` (28min — diverge do mock, que assume 300s) |
| `POST auth/oauth/v2/token` scope `pix.cambio.payment.order` | ✅ `200`, token emitido |
| `GET core2/pix/cambio/v1/collection-orders?DateUtc=...` (list, read-only) | 🔴 `403` corpo vazio |
| `GET core2/pix/cambio/v1/payment-orders?DateUtc=...` (list, read-only) | 🔴 `403` corpo vazio |

**Achado novo desta sessão (mais amplo que o gap já documentado):** o client demo emite token
válido para ambos os scopes, mas **não tem acesso a nenhum recurso** de
`core2/pix/cambio/v1/{collection-orders,payment-orders}` — nem leitura (`GET` list), só auth.
O gap conhecido em `PROVIDER-MAP.md` (`403`/`CT02098` em `POST` create) já registrava bloqueio de
escrita; aqui confirmamos que **até a listagem de leitura está bloqueada** pelo mesmo motivo de
provisionamento (client BS2 sandbox não vinculado ao contexto/conta que expõe esses recursos).
Isso não é um bug do SDK a construir — é um bloqueio externo de provisionamento BS2 que precede
qualquer teste E2E real, inclusive os não financeiros de leitura.

**Confirmação adicional (2026-07-15):** repetido o mesmo probe (`GET collection-orders`) com os
clients que o **legado `cerebro` efetivamente usa hoje** — `pass cambio-real/bs2/cerebro-demo-env`
(mesmo `client_id`/`client_secret` do `sandbox-env` usado acima — credencial duplicada em dois
caminhos do `pass`, não um terceiro client) e `pass cambio-real/bs2/demo-env` (client distinto) —
para descartar "credencial errada/mal configurada" como causa. Mesmo resultado nos dois clients
realmente distintos: autenticam (`200`), `403` em qualquer recurso. Isso isola o bloqueio no
ambiente/contexto de conta sandbox da BS2 como um todo, não em uma credencial específica.

## 4. Payin — collection-order (`pix.cambio.collection.order`)

Fonte de verdade: `cerebro/app/Libraries/Bs2/{AbstractService,PixService,PayinNotification}.php`
(legado, modo `legacy`, o modo default e o único auditado como "mais confiável" pelo próprio gate
doc do `synapse`). O adapter novo (`app/Integration/Adapters/BS2/BS2Adapter.php`, modo
`proxy`/`hub`) e o greenfield C# (`Bs2PixAdapter.cs`) são tratados como **hipótese não
confirmada**, mesmo nível de confiança um do outro.

### Create — `POST core2/pix/cambio/v1/collection-orders`

```
amount: float
externalId: string
information: string
transactionReason: 1                     // sempre 1
creditorDebtorType: "01" | "05"          // 01=CPF, 05=CNPJ
debtor:
  financialInstitution: string
  issuer: string
  account: string
  accountType: "Current" | "Savings"
  cde: true                              // sempre true no fluxo CDE
  ibanCode: string
  identification: string
  identificationType: "CPF" | "CNPJ"     // maiúsculo — código sempre envia assim; mock de resposta usa "Cnpj" mas é só formatação de exibição, não confiar nele para o request
  name: string
foreignCreditor:
  country: string
  name: string
```

Resposta: **string simples** (o id da ordem), não um objeto JSON. Diverge da suposição do
`BS2Adapter`/greenfield C# (`{id|uuid|orderId|data.id}`).

### Get details — `GET core2/pix/cambio/v1/collection-orders/{orderId}`

```
id, externalId, createdDate
transaction:
  paymentDate, amount, paymentType: "DebtorCDE", status, statusInformation,
  information, endToEndId, qrCode        // QR fica aninhado em transaction.qrCode, não top-level
debtor: {...}
foreignCreditor: { name, country }
classification: { natureFact, clientCode, natureGroup, natureGuarantee, payerReceiverCode, otherEspecifications }
```

Sem campo `expiration` retornado pela BS2 — o legado computa expiração client-side
(`now()+15min`). O gateway deve fazer o mesmo (ou usar o padrão canônico de `InstructionTtlMinutes`
do greenfield, 60min, mas documentar a divergência do 15min legado — decisão a confirmar antes de
codificar, ver §7).

### List — `GET core2/pix/cambio/v1/collection-orders`

Query: `DateUtc` (Y-m-d), `CurrentPage`, `QuantityPerPage`.
Resposta paginada: `{ currentPage, pageSize, totalRecords, totalPages, itens, previousPage, nextPage }`
— **`itens`**, não `items`. Bloqueado por 403 no client sandbox atual (ver §3).

### Cancel — `DELETE core2/pix/cambio/v1/collection-orders/{orderId}`

Cancela QR code expirado. Efeito `destructive`.

### Status enum (payin)

| BS2 `transaction.status` | Mapeamento interno |
|---|---|
| `Issued`, `QrCodeGenerated` | PENDING |
| `Succeed` | PAID |
| `Failed`, `RequestedCancel`, `Canceled` | ERROR/cancelled |

## 5. Payout — payment-order (`pix.cambio.payment.order`)

Fonte: `cerebro/app/Libraries/Bs2/{PayoutService,PayoutNotification}.php`.

### Create via PIX key — `POST core2/pix/cambio/v1/payment-orders/dict-key`

```
amount, externalId, information, transactionReason: 1, creditorDebtorType
creditor: { cde: true, ibanCode }             // só isso — sem name/bankCode/account aqui
creditorDict: { dictKey, dictKeyType }
foreignDebtor: { country, name }
```

### Create via conta bancária — `POST core2/pix/cambio/v1/payment-orders/account-data`

```
amount, externalId, information, transactionReason: 1, creditorDebtorType
creditor:
  financialInstitution, issuer, account, accountType: "Current"|"Savings",
  cde: true, ibanCode, identification, identificationType: "CPF"|"CNPJ", name
foreignDebtor: { country, name }
```

**Correção confirmada vs. o adapter C# greenfield** (`Bs2SettlementAdapter.cs`, que assumia
`creditor.{name,bankCode,accountNumber,routingNumber,bicCode}` como "plausible best guess"): os
nomes reais são `financialInstitution` (não `bankCode`), `issuer` (não `routingNumber`), `account`
(não `accountNumber`); **não existe `bicCode`**. `ibanCode` é o código IBAN-like da **própria
conta BS2 do cliente** (vem da config, não do beneficiário). Mesmo endpoint é reusado para
**refund** (`PixService::refund`), remapeando `bank`→`financialInstitution`,
`bank_branch`→`issuer`.

### Status / get — `GET core2/pix/cambio/v1/payment-orders/{id}`

Sem endpoint de status em lote — confirmado, batem com a suposição do greenfield. Cada
payment-order tem status individual.

| BS2 `transaction.status` | Mapeamento (`PayoutService::checkPayed`) |
|---|---|
| `Succeed` | completed |
| `Issued` | pending |
| `Initialized`, `Confirmed` | pending, `error='delivered'` |
| `Failed` | error = `'Erro - ' + statusInformation` |

`PayoutNotification::handle` refina: `error==='delivered'` → `PROCESSING`; `error` iniciando com
`'Erro'` → `CANCELED` + alerta Discord; prefixo de erro não reconhecido → exceção não tratada
(gap operacional do legado, não replicar — o novo gateway deve tratar como `ProblemDetail`
genérico em vez de deixar a exceção estourar).

### List — `GET core2/pix/cambio/v1/payment-orders`

Mesmo formato paginado do payin (`itens`). Bloqueado por 403 (ver §3).

## 6. Webhooks

- **Auth**: HMAC-SHA256 sobre o corpo bruto, header `X-BS2-Signature`, comparação
  `hash_equals(hash_hmac('sha256', $body, $secret))`. **E** allowlist de IP de origem — ambos
  precisam passar. Secret: `cambio-real-v2/providers/bs2-webhook-secret`.
- **Discriminador payin vs payout**: `event` iniciando com `payment.order.` = payout; qualquer
  outro valor = payin.
- **Vocabulário de status do payload do webhook em si é não confiável** — duas implementações
  divergem (`WebhookHandler::isSuccessfulPayin`, battle-tested, espera inglês
  `PAGO|PAID|SUCCEED|SUCCEEDED|SUCCESS|COMPLETED|CONFIRMED`; `BS2WebhookNormalizer`, não
  confirmado, espera português `PAGO|PENDENTE|EXPIRADO|CANCELADO|DEVOLVIDO`). **Decisão para o
  gateway novo**: tratar o webhook só como **gatilho de re-poll**, nunca como fonte de verdade do
  status — replicar o padrão do legado (`PayinNotification`/`PayoutNotification` sempre chamam
  `GET details`/`checkPayed()` após receber o webhook), não o padrão do `BS2WebhookNormalizer`.
- **Payout: resolução de external-id por prefixo** (`PayoutNotification::getTransactionId`,
  ADR-0013 já implementado no legado — fail-closed, não é uma divergência a corrigir, é o
  comportamento correto a preservar num nível equivalente no gateway se ele vier a rotear payouts
  de volta a um sistema consumidor — fora de escopo do SDK/gateway BS2 em si, é lógica do
  `cambio-real-v3`/`cerebro`, não deste serviço standalone. Documentado aqui só para contexto,
  **não será replicado no gateway BS2** — o gateway expõe o payout normalizado; a resolução de
  qual entidade de negócio ele pertence é responsabilidade do consumidor, igual ao padrão
  Kira/Ripple).

## 7. Erros

- Sem catálogo de códigos BS2 no legado. `CT02098` (`Client not located in context`, HTTP 400) é
  atestado só como falha real de sandbox em `synapse/docs/integration-assessment/20-bs2-real-sandbox-flow-gate.md`
  (2026-06-07), não modelado em código.
- Corpo de erro tem **duas formas observadas**: `{ message }` / `{ 0: { description } }` (usado
  defensivamente no legado) e `[{ tag, descricao }]` (fixture de mock, array de objetos, chave em
  português). O SDK deve suportar ambas as formas ao extrair a mensagem de erro — não assumir uma
  única forma.
- `error_code` no legado é sempre o HTTP status, não um código BS2 granular — não há mapeamento
  máquina-legível de código→causa hoje. O gateway novo deve preservar o HTTP status BS2 original
  dentro de `ProblemDetail.code`/extensões, já que é a única granularidade disponível.

## 8. Idempotência, retry, polling

- **Idempotency-Key**: o legado **não envia** esse header — idempotência vem só do `externalId`
  único por transação. O header `Idempotency-Key: bs2:{payin|payout}:{external_id}` só existe no
  adapter novo não confirmado (`BS2Adapter`). **Decisão**: o SDK BS2 vai enviar o header mesmo
  assim (alinhado à regra canônica do goal — "idempotency key quando suportado" — como
  best-effort; BS2 provavelmente ignora um header desconhecido em vez de rejeitar, mas isso não
  foi confirmado com uma chamada de escrita real, que está bloqueada por §3). Marcar como
  suposição a validar quando o provisionamento de escrita for liberado.
- **Polling de QR (payin)**: legado hardcoded 10 tentativas / 1s. Bate com o default do greenfield
  C# (`DetailsPollTries=10`). O adapter novo não confirmado (`BS2Adapter`) diverge (30 tentativas
  configurável) — **não seguir essa divergência**, usar 10/1s como default, configurável.
- **Retry em auth**: nenhum no legado. O SDK novo implementará single-flight de token
  (`I Bs2TokenProvider` + `DelegatingHandler`) com cache por scope e no máximo 1 retry em 401 pós
  token, conforme padrão canônico do goal — isso é uma melhoria sobre o legado, não uma cópia dele.

## 9. Matriz de cobertura — todos os endpoints

| # | Endpoint upstream | Método | Recurso SDK | Endpoint gateway | Perfil PP | Efeito | Fixture/cleanup | Teste esperado | Status sandbox |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `auth/oauth/v2/token` (scope collection.order) | POST | `Bs2TokenProvider` | interno (não exposto) | Sync (auth) | read/non-financial | n/a | unit (mock) + integração sandbox opt-in | ✅ validado ao vivo 2026-07-15 |
| 2 | `auth/oauth/v2/token` (scope payment.order) | POST | `Bs2TokenProvider` | interno (não exposto) | Sync (auth) | read/non-financial | n/a | unit (mock) + integração sandbox opt-in | ✅ validado ao vivo 2026-07-15 |
| 3 | `core2/pix/cambio/v1/collection-orders` | POST | `CollectionOrdersResource.CreateAsync` | `POST /v1/bs2/payins` | Sync (submit) | financial-write | fixture + contrato; sandbox só com autorização explícita | contrato (sucesso/validação/erro) + sandbox bloqueado (pendente autorização E pendente provisionamento BS2) | 🔴 bloqueado (provisionamento; ver §3) |
| 4 | `core2/pix/cambio/v1/collection-orders/{id}` | GET | `CollectionOrdersResource.GetAsync` | `GET /v1/bs2/payins/{id}` | Sync (status) | read | depende de #3 existir | contrato + sandbox (opt-in) | 🔴 bloqueado transitivamente (depende de #3) |
| 5 | `core2/pix/cambio/v1/collection-orders` (list) | GET | `CollectionOrdersResource.ListAsync` | `GET /v1/bs2/payins` | Sync (status, agregado) | read | n/a | contrato + sandbox (opt-in, seguro) | 🔴 `403` confirmado ao vivo — provisionamento BS2 pendente, não é bug do SDK |
| 6 | `core2/pix/cambio/v1/collection-orders/{id}` | DELETE | `CollectionOrdersResource.CancelAsync` | `DELETE /v1/bs2/payins/{id}` | Sync (cancel) | destructive | depende de #3 | contrato/fixture; sandbox só com autorização explícita | 🔴 bloqueado transitivamente |
| 7 | `core2/pix/cambio/v1/payment-orders/dict-key` | POST | `PaymentOrdersResource.CreateByPixKeyAsync` | `POST /v1/bs2/payouts/pix-key` | Sync (submit) | financial-write | fixture + contrato; sandbox só com autorização explícita | contrato + sandbox bloqueado (autorização + provisionamento) | 🔴 bloqueado (provisionamento) |
| 8 | `core2/pix/cambio/v1/payment-orders/account-data` | POST | `PaymentOrdersResource.CreateByAccountAsync` | `POST /v1/bs2/payouts/account` | Sync (submit) | financial-write | idem | idem | 🔴 bloqueado (provisionamento) |
| 9 | `core2/pix/cambio/v1/payment-orders/account-data` (reuso p/ refund) | POST | `PaymentOrdersResource.RefundAsync` | `POST /v1/bs2/payouts/{id}/refund` | Sync (refund) | financial-write | idem | idem | 🔴 bloqueado (provisionamento) |
| 10 | `core2/pix/cambio/v1/payment-orders/{id}` | GET | `PaymentOrdersResource.GetAsync` | `GET /v1/bs2/payouts/{id}` | Sync (status) | read | depende de #7/#8 | contrato + sandbox (opt-in) | 🔴 bloqueado transitivamente |
| 11 | `core2/pix/cambio/v1/payment-orders` (list) | GET | `PaymentOrdersResource.ListAsync` | `GET /v1/bs2/payouts` | Sync (status, agregado) | read | n/a | contrato + sandbox (opt-in, seguro) | 🔴 `403` confirmado ao vivo — mesmo bloqueio de #5 |
| 12 | webhook inbound payin | POST (inbound) | n/a (gateway-side, `Bs2WebhookAuthenticator`) | `POST /v1/bs2/webhooks/payin` | Sync (event) | non-financial-write (só dispara re-poll) | payload sintético assinado p/ teste de contrato | contrato HMAC válido/inválido + normalização | ⚪ não testado (precisa endpoint público implantado) |
| 13 | webhook inbound payout | POST (inbound) | n/a | `POST /v1/bs2/webhooks/payout` | Sync (event) | non-financial-write | idem | idem | ⚪ não testado |
| 14 | `pj/apibanking/forintegration/v2/contascorrentes/saldo` | GET | `AccountsResource.GetBalanceAsync` | `GET /v1/bs2/accounts/balance` | Sync (status) | read | n/a | contrato (mock) + sandbox opt-in | 🔴 bloqueio de provisionamento esperado (mesmo escopo de #5), não exercitado ao vivo nesta sessão |
| 15 | `pj/apibanking/forintegration/v2/contascorrentes/extrato` | GET | `AccountsResource.GetStatementAsync`/`GetFullStatementAsync` | `GET /v1/bs2/accounts/statement` | Sync (status, agregado) | read | n/a | contrato (mock) + sandbox opt-in | 🔴 bloqueio de provisionamento esperado, não exercitado ao vivo nesta sessão |
| 16 | `pj/apibanking/forintegration/v2/contascorrentes/extrato/analitico` | GET | `AccountsResource.GetStatementAnalyticalAsync` | `GET /v1/bs2/accounts/statement/analytical` | Sync (status, agregado) | read | n/a | contrato (mock) + sandbox opt-in | 🔴 bloqueio de provisionamento esperado, não exercitado ao vivo nesta sessão |

**Resumo do gate de leitura não financeira desta iteração:** dos 13 endpoints do escopo declarado
original (payin/payout/webhooks), **2 foram exercitados com sucesso ao vivo** (auth, ambos os
scopes) e **2 foram exercitados e retornaram bloqueio externo confirmado** (list payin/payout,
`403`). Os demais 9 dependem de escrita financeira (bloqueada por autorização pendente, per goal
§0.5) ou de dados que só existem após uma escrita bem-sucedida (bloqueados transitivamente) ou de
infraestrutura do gateway ainda não implantada (webhooks). Os endpoints #14-16 (conta corrente,
ver §13) foram **adicionados nesta sessão** como gap P0 fora do escopo declarado original — READ,
não financeiros, implementados e cobertos por contrato/mock; sandbox real não exercitado (mesmo
bloqueio de provisionamento esperado do escopo #3-#11).

## 10. Lacunas, suposições e riscos

1. **Bloqueio de provisionamento BS2 é o risco P0**, não uma lacuna de código: o client sandbox
   atual (`cambio-real-v2/providers/bs2/sandbox-env`) emite token válido mas não tem acesso a
   nenhum recurso `collection-orders`/`payment-orders`, nem leitura. Isso bloqueia toda a
   cobertura real de sandbox (não só a financeira) até a BS2 vincular o client ao contexto de
   conta correto. Ação: reportar ao dono do projeto — precisa de contato com BS2, não é
   resolvível por código.
2. Casing de `identificationType` (`CPF`/`CNPJ` maiúsculo no request) confirmado pelo código, não
   pelo mock — mock tem `Cnpj` só como valor ilustrativo em resposta, não é o formato de request
   real. SDK deve serializar sempre maiúsculo.
3. TTL de expiração do QR: legado usa 15min hardcoded; padrão canônico Kira/Ripple/greenfield usa
   configurável. Decisão pendente de confirmação antes de implementar (não bloqueia discovery,
   mas deve ser resolvida na fase de design do SDK — provavelmente configurável com default
   15min para bater com o comportamento real observado).
4. Sem catálogo de erro BS2 formal — SDK deve ser defensivo (aceitar as duas formas de corpo de
   erro observadas) em vez de assumir um schema único.
5. Efeito de `Idempotency-Key` em escrita real é desconhecido (BS2 pode ignorar ou rejeitar) —
   só pode ser confirmado quando o provisionamento de escrita for liberado.
6. `BS2AdapterV2.php` (legado, modo hub) usa endpoint `v2/transactions` não documentado em
   nenhum outro lugar — tratado como stub/placeholder, **não usado como referência**.

## 11. Limites de responsabilidade SDK / gateway / plataforma

- **SDK (`bs2-sdk`)**: modela exatamente os payloads BS2 acima (payin/payout/webhook), sem
  normalização de campos, sem conhecimento de `Envelope<T>`. Zero dependência de
  `CambioReal.Contracts`. Replica a decisão de "webhook só como gatilho de re-poll" internamente
  no `Resources/` (um método `ConfirmViaPollingAsync` que um consumidor do webhook pode chamar),
  mas não implementa o handler HTTP do webhook em si — isso é responsabilidade do gateway
  (recebe, autentica HMAC, extrai external-id, e decide o que fazer, igual ao padrão Kira/Ripple
  onde o SDK nunca expõe endpoints HTTP de entrada).
- **Gateway (`bs2-gateway`)**: expõe `/v1/bs2/{payins,payouts}` e os dois webhooks inbound,
  traduz toda resposta para `Envelope<T>`/`ProblemDetail`, preserva o HTTP status original da BS2
  como metadado de erro (não há código BS2 granular a mapear). Não replica a lógica de resolução
  de external-id por prefixo do `cerebro` (fora de escopo — é lógica de negócio do consumidor do
  v3, não do gateway do provider).
- **Plataforma (`cambio-real-v3` / consumidor)**: decide o que fazer com o resultado normalizado
  do gateway (roteamento por prefixo, contabilização, etc.) — mesmo modelo já adotado para
  Ripple (`ripple-gateway` "ainda não consumido pelo v3" é aceito como gap conhecido, não deste
  serviço).

## 12. Nenhuma contradição arquitetural encontrada

BS2 se encaixa integralmente no padrão canônico `Sync` + SDK/gateway standalone já usado por
Kira/Ripple. Não é necessário abrir ADR de exceção. O único ADR relevante ao domínio (ADR-0013,
resolução de prefixo de payout) pertence ao `cerebro`/`cambio-real-v3`, não a este SDK/gateway,
e não será portado.

## 13. Conta corrente — saldo/extrato (achado fora do escopo original, agora implementado)

`provider-protocol/docs/gateways/coverage/bs2.md` registrou este achado como um gap não decidido
explicitamente (nem gap, nem exclusão intencional — "não contava" no cálculo de cobertura por estar
fora do domínio PIX payin/payout). Esta seção fecha essa lacuna: os 3 endpoints foram implementados
como gap P0 (READ-only, consulta para conciliação).

Fonte de verdade: `cerebro/app/Libraries/Bs2/AccountService.php` (legado), usado hoje por
`Bs2ReconciliationCommand` (concilia payin/payout contra o extrato bancário, envia relatório por
e-mail) e por `Bs2BankFeeAccountingCommand`/`Bs2MarlimAccountingCommand` (contabilização).

- **Domínio distinto**: `pj/apibanking/forintegration/v2/contascorrentes/*` (banking/tesouraria),
  não `core2/pix/cambio/v1/*` (PIX câmbio). Mesmo host/ambiente, path diferente.
- **Escopo OAuth2 reusado, não um escopo próprio**: confirmado em
  `AccountService::__construct()` — `$this->scope = 'pix.cambio.collection.order'`, o MESMO
  escopo do payin. O SDK roteia estas consultas pelo mesmo `HttpClient`/pipeline de
  `CollectionOrdersResource` (`Bs2Client.GetAccountsAsync` → `collectionOrdersHttpClient`), sem
  exigir um terceiro cliente HTTP.

### Saldo — `GET pj/apibanking/forintegration/v2/contascorrentes/saldo`

Sem parâmetros de query — confirmado (`AccountService::balance()`). **Shape de resposta NÃO
confirmado**: `config/bs2-mock.php` não tem entrada `balance` (`mock_path` aponta para
`bs2-mock.balance.success`, ausente do arquivo de mocks) e o método nunca é chamado por nenhum
outro lugar do `cerebro` (confirmado por grep em toda a árvore `app/`). O SDK modela
`Bs2AccountBalance` inferindo o shape do bloco `saldo` aninhado devolvido por `GET .../extrato`
(mesmo domínio, mesmo nome de campo) — tratar como suposição a confirmar quando o endpoint puder
ser exercitado ao vivo.

### Extrato — `GET pj/apibanking/forintegration/v2/contascorrentes/extrato`

Query confirmada (`AccountService::statement()`): `movimentoInicial`/`movimentoFinal`
(`Y-m-d`), `inicio` (offset de paginação). **Shape de resposta CONFIRMADO** campo a campo em
`config/bs2-mock.php` (chave `statement.success`) e no uso real de
`Bs2ReconciliationCommand::fetchBs2Statement`: paginação por `inicio += 100` até `inicio >= total`
(o SDK expõe isso como `AccountsResource.GetFullStatementAsync`, parametrizável, sem replicar o
retry-com-sleep em resposta vazia do legado — decisão de orquestração de job, fora do SDK, mesmo
padrão já usado para não replicar a resolução de external-id por prefixo do webhook em §6).
Categorias de movimentação usadas pelo legado para conciliar (`Bs2ReconciliationCommand`
constantes): `2`=TED recebido, `10`=PIX recebido, `11`=PIX enviado, `16`=PIX recebido interno,
`17`=PIX enviado interno, `20`=tarifa — não modelado como enum (catálogo não confirmado como
exaustivo).

### Extrato analítico — `GET pj/apibanking/forintegration/v2/contascorrentes/extrato/analitico`

Query confirmada (`AccountService::statementV2()`): `dataInicial`/`dataFinal`, formato
`Y-m-d H:i` (**com hora**, diferente do extrato simples, que é só data) — sem parâmetro de
paginação (`inicio` não existe aqui). **Shape de resposta NÃO confirmado**: mesma situação do
saldo — sem fixture em `config/bs2-mock.php` (`mock_path` = `bs2-mock.statementV2.success`,
ausente) e sem nenhuma chamada ao método em outro lugar do `cerebro`. O SDK devolve o corpo cru
como `JsonElement` (`AccountsResource.GetStatementAnalyticalAsync`) em vez de inventar um schema
sem evidência — mesmo padrão de campo "cru" já usado no `ouribank-sdk` para formas de provider
desconhecidas.

### Testes

- Contrato/mock: `tests/CambioReal.Bs2.Client.Tests/AccountsResourceTests.cs` — cobre path/query
  exatos dos 3 endpoints, parsing de `Bs2AccountStatement`/`Bs2AccountMovement`
  (remetente/favorecido), paginação de `GetFullStatementAsync` e o retry de 401 no pipeline
  reusado. Passam sem depender de rede.
- Sandbox opt-in: `tests/CambioReal.Bs2.Client.SandboxTests/Bs2SandboxTests.cs` —
  `AccountBalance_ReachesProviderAndReportsCurrentAccessStatus`/
  `AccountStatement_ReachesProviderAndReportsCurrentAccessStatus`, mesmo padrão de sensor de
  regressão dos testes de payin/payout (nunca falha por causa do status HTTP em si, só por erro de
  rede/protocolo). **Não exercitados ao vivo nesta sessão** — o mesmo bloqueio de provisionamento
  do §3 é esperado (client sandbox não vinculado ao contexto de conta correto), e a BS2 confirmou
  externamente que os merchants de sandbox estão bloqueados (403) além do bloqueio já documentado.
  Confirmação real fica pendente de desbloqueio externo + execução autorizada pelo dono.
