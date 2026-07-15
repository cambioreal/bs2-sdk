# CambioReal.Bs2.Client.SandboxTests

Testes de integração sandbox real, opt-in — nunca incluído em `Bs2.slnx`, nunca rodado por padrão
em CI (goal-loop §2.5).

## Rodar

```bash
set -a
eval "$(pass show cambio-real-v2/providers/bs2/sandbox-env | sed -n \
  's/^CLIENT_ID=/BS2_SANDBOX_CLIENT_ID=/p;s/^CLIENT_SECRET=/BS2_SANDBOX_CLIENT_SECRET=/p')"
set +a
dotnet test tests/CambioReal.Bs2.Client.SandboxTests/CambioReal.Bs2.Client.SandboxTests.csproj
unset BS2_SANDBOX_CLIENT_ID BS2_SANDBOX_CLIENT_SECRET
```

Sem as duas variáveis de ambiente, os testes falham explicitamente — não há skip silencioso.

## Última execução ao vivo — 2026-07-15

```
Passed CambioReal.Bs2.SandboxTests.Bs2SandboxTests.PaymentOrderScope_AuthenticatesLiveAgainstSandbox [740 ms]
  pix.cambio.payment.order: token issued, length=36 (masked).
Passed CambioReal.Bs2.SandboxTests.Bs2SandboxTests.CollectionOrderScope_AuthenticatesLiveAgainstSandbox [154 ms]
  pix.cambio.collection.order: token issued, length=36 (masked).
Passed CambioReal.Bs2.SandboxTests.Bs2SandboxTests.CollectionOrdersList_ReachesProviderAndReportsCurrentAccessStatus [666 ms]
  GET collection-orders: HTTP 403 (Forbidden) — (sem descrição)

Passed: 3, Failed: 0, Total: 3
```

Confirma, através do SDK real (não `curl` manual): autenticação funciona para os dois escopos;
qualquer acesso a recurso (mesmo leitura) segue bloqueado por `403` — bloqueio de provisionamento
BS2 (externo), não um bug de código. `CollectionOrdersList_ReachesProviderAndReportsCurrentAccessStatus`
nunca falha por causa do `403` em si (só por erro de rede/protocolo) — é um sensor de regressão: se
o provisionamento for corrigido, o teste passa a imprimir `200 OK` e sinaliza "expandir cobertura".
