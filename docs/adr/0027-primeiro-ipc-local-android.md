# ADR-0027 — Primeiro IPC local autenticado entre Hub e Lunet

## Status

Proposto — aguarda **DEC-0037**. Nenhum transporte, endpoint, permissão Android ou política de confiança desta ADR entra em vigor antes da decisão do proprietário.

## Contexto

P5-3 estabilizou a Host API v1 como contrato semântico neutro de transporte (ADR-0024 / DEC-0034-A). O runtime local do Hub já executa a capability estável `text.inspect@1.0.0` e sua matriz pública de conformance.

O bloqueio de consumidor real deixou de existir: a Fase 3 do Lunet foi aprovada pelo proprietário em LUNET-303, Issue #204. O Lunet possui Product Shell Android real, identidade de aplicativo própria e continua funcionando/distribuindo sem Hub.

P5-4 exige um ensaio **interprocessos real** para autenticação, disconnect, cancelamento, timeout e limites. O primeiro cenário concreto é deliberadamente opcional: o Lunet invoca `text.inspect@1.0.0` hospedada fora de seu processo para provar o binding da Host API. Se o Hub estiver ausente, a integração aparece indisponível e o Lunet continua íntegro no seu domínio essencial.

A comparação técnica está em `docs/architecture/ipc-transport-readiness.md` e cobre chamada em processo, Android Bound Service/Binder, Unix local socket e TCP loopback.

## Problema

Escolher o primeiro transporte e o modelo de confiança sem:

- transformar Hub em dependência essencial do Lunet;
- aceitar package id, UID ou identidade enviados pelo payload;
- tratar localhost como autenticação;
- re-assinar Products apenas para facilitar uma permission `signature`;
- congelar Binder como protocolo universal do Ecosystem;
- extrair runtime/Tool antes da Extraction Review;
- misturar grants de capability com confiança de instalação/pareamento.

## Opções

### A — Android Bound Service/Binder + identidade do SO + pareamento por instalação

O Hub hospeda um serviço Android opcional. O Lunet é o primeiro caller.

A sessão só abre depois de:

1. capturar o UID real da transação Binder antes de qualquer trabalho assíncrono;
2. resolver os pacotes associados ao UID e conferir package id + signer pela política local;
3. quando o signer atual não constituir confiança suficiente, usar identidade por instalação: chave assimétrica não exportável gerada no Android Keystore, pareamento inicial confirmado pelo usuário e challenge de posse por sessão;
4. o cliente verificar também provider/pacote/signer/chave pareada antes de enviar dados sensíveis;
5. vincular sessão, callbacks, grants, cancelamento e revogação à identidade autenticada;
6. falhar fechado em qualquer inconsistência.

Binder fornece lifecycle nativo de bind/unbind e morte do peer. Framing e protocolo de transporte ficam restritos ao adapter Android; a Host API continua canônica e neutra.

### B — Unix local socket + credenciais do peer + pareamento criptográfico

Usa socket local e credenciais do SO quando disponíveis, complementadas pelo mesmo princípio de pareamento por instalação. Exige provar alcance real entre sandboxes Android/SELinux, ownership do endpoint, framing, backpressure, reconexão e cleanup.

### C — TCP loopback + autenticação mútua por chaves pareadas

Usa uma porta local e protocolo de aplicação autenticado bilateralmente. É conceitualmente mais portável, mas `127.0.0.1` não prova identidade. Introduz descoberta de porta, framing, parser, credenciais, replay protection, budgets e rate limiting próprios.

### D — Adiar IPC e manter apenas adapter em processo

Permite avançar parcialmente na hospedagem da mesma Tool dentro do Lunet sem transporte interprocessos. P5-4 permanece incompleto e o gate da Fase 5 continua bloqueado.

## Decisão

**Pendente — DEC-0037.**

Recomendação técnica atual: **Opção A**.

A recomendação é específica ao primeiro alvo Android. Não significa que Binder será usado por Urbe Web, Windows ou futuros Hosts.

## Consequências se A for escolhida

- primeiro provider IPC: endpoint opcional do Hub Android;
- primeiro caller: Lunet Android;
- capability do ensaio: `text.inspect@1.0.0`;
- Host API v1 permanece a semântica canônica;
- Product não referencia `Hub.Core`;
- ausência/falha do Hub só torna a capability remota indisponível;
- instalação não concede grant; trust de peer e permissions/grants são camadas distintas;
- signer de desenvolvimento não é promovido silenciosamente a prova de autoria;
- pareamento por instalação fica local ao adapter Android enquanto não houver evidência para extração;
- IPC-01..IPC-18 precisam rodar com dois processos/APKs reais;
- budgets de payload, fila, sessões, progresso, deadline e idle timeout são fixados antes do gate;
- P5-5 ainda exige a mesma Tool em dois Hosts reais e Extraction Review positiva antes de mover implementação para componente compartilhado.

## Segurança e lifecycle

A autorização efetiva segue esta ordem:

`peer do SO → política/pairing do adapter → sessão Host API → Context → grants → capability/version/input`.

Nenhuma camada posterior pode elevar uma anterior.

Morte do peer, unbind, package replacement incompatível, signer diferente, chave pareada perdida, revogação ou fechamento invalidam a sessão. Reconectar cria sessão nova e nunca restaura grants anteriores automaticamente.

Cancelamento precisa de caminho de controle independente da fila de trabalho. Deadline limita a espera do caller, mas não promete preempção física de provider não cooperativo.

## Alternativas rejeitadas

Nenhuma enquanto DEC-0037 estiver pendente. A decisão do proprietário preencherá esta seção e mudará o status para Aceito se aplicável.

## Referências

MANIFEST §§6, 14–16, 29; NN-003, NN-006, NN-007, NN-010, NN-011, NN-015, NN-016, NN-017, NN-018, NN-020, NN-022, NN-023; ADR-0011; ADR-0012; ADR-0024; `docs/architecture/capability-runtime.md`; `docs/architecture/ipc-transport-readiness.md`; `docs/contracts/host-api.v1.json`; `docs/contracts/capabilities/text.inspect.json`; ROADMAP P5-4..P5-7; Issue #194; LUNET-303/Issue #204; DEC-0037.
