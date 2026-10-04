# P5-4 — Validação Android do IPC Binder autenticado

Estado: **candidato em construção; NÃO aprovar ainda**.

Este é o objeto da validação crítica do P5-4. A prova combina conformance/CI portátil, build real dos dois APKs e um ensaio DEVICE apenas para propriedades que dependem do Android. Nenhum caso DEVICE é convertido em “passou” por inferência e nenhum mecanismo de diagnóstico é adicionado ao produto apenas para fabricar uma prova.

## Candidatos

- Hub: **a definir após CI verde do head final do PR #210**.
- Caller Android: **a definir após CI verde do head final do PR #210**.
- Commit testado: **a definir após estabilizar o PR #210**.
- Binding: `docs/contracts/bindings/android-binder-v1.md`.
- Trust policy: `docs/contracts/bindings/android-binder-v1.trust.json`.

## Matriz IPC-01..IPC-18

| ID | Evidência já automatizável | Evidência Android necessária | Estado |
|---|---|---|---|
| IPC-01 | `AuthenticatedPeerCanOpenDiscoverInvokeAndClose`; Host API v1; `text.inspect@1.0.0` | Dois APKs/processos: parear e obter contagens corretas por Binder | Pendente DEVICE |
| IPC-02 | Allowlist bilateral package+signer; provider deriva caller de `Binder.CallingUid` e só então aplica a política | Confirmar o caller candidato real; negativa de signer é propriedade de policy/code e não exige um APK rogue permanente | Parcial CI + DEVICE positivo |
| IPC-03 | O wire não aceita actor/UID/pacote efetivo; `SessionIdFromAnotherPeerNeverCrossesIdentityBoundary` | Confirmar sessão real criada pelo UID do caller candidato | Parcial CI + DEVICE |
| IPC-04 | Caller filtra provider por package+signer antes do bind efetivo, pinça chave desde o código humano e verifica challenge ECDSA | Reinstalação/rotação de chave deve exigir novo pareamento e nunca enviar input silenciosamente | Pendente DEVICE |
| IPC-05 | O binding inicial não possui callback/progress remoto; invoke é request/reply síncrono | Nenhuma. Se callback/event wire for introduzido, IPC-05 reabre antes do gate | N/A justificado |
| IPC-06 | `SessionIdFromAnotherPeerNeverCrossesIdentityBoundary`; cancel/close/revoke exigem peer proprietário | Nenhuma adicional para semântica; UID real é coberto por IPC-01/03 | CI |
| IPC-07 | `UndeclaredOrUngrantedPermissionIsNeverInherited`; discovery filtra scope/grants | Discovery real deve mostrar somente `text.inspect@1.0.0` no Context do caller | Parcial CI + DEVICE |
| IPC-08 | Context e grants são definidos em `android-binder-v1.trust.json` pelo Host; `session.open` **rejeita** `ContextJson` do payload; não existe operação para adicionar grant | Confirmar sessão real abre no fluxo permitido; nenhuma edição de Context é oferecida ao caller | CI + DEVICE positivo |
| IPC-09 | `InvalidEnvelopesNeverReachTheTool`, `InputRejectsUnknownFieldsWrongTypeAndOversizedText`, `InvalidInputVersionAndProviderFailureRemainSanitized` | Nenhuma propriedade Android específica | CI |
| IPC-10 | JSON <= 256 KiB; Parcel <= 640 KiB; caller mede `DataSize()`; provider mede `DataAvail()` antes de ler a string; parser mantém limite de profundidade | IPC-01 confirma o caminho Binder real; limites negativos ficam em CI/inspeção do binding para não exigir payload abusivo no aparelho do usuário | CI + DEVICE positivo |
| IPC-11 | `DuplicateRequestDoesNotExecuteAgainAndJournalIsBounded`; `DuplicateRequestIsRejectedWithoutSecondProviderEffect`; nova sessão não herda request IDs | Nenhuma propriedade Android adicional | CI |
| IPC-12 | `CancelUsesControlPathAndLateSuccessIsDiscarded`; LocalHostSession descarta tardios | Nenhuma propriedade Android adicional; Binder permite transações concorrentes e cancel é operação própria | CI |
| IPC-13 | `RevokingGrantCancelsActiveCallAndCannotRestorePrivilege`; `RevokeDuringExecutionCancelsAndRemovesCapability`; Binder expõe `revoke` que só reduz grants Host-owned. Pareamento também pode ser revogado no Hub | Revogar o pareamento no Hub e provar que próxima sessão exige novo pareamento | Parcial CI + DEVICE |
| IPC-14 | `DeadlineIsTransportFailureAndSessionRemainsUsable`; `NonCooperativeProviderCannotHoldCallerPastDeadline`; default 10 s, máximo 30 s | Nenhuma propriedade Android adicional | CI |
| IPC-15 | Token Binder do caller em `session.open`; provider usa `LinkToDeath`; gateway fecha/cancela sessão; idle timeout 2 min é fallback | Matar o processo do caller com sessão mantida e confirmar encerramento; matar provider deve virar indisponibilidade sem quebrar caller | Pendente DEVICE |
| IPC-16 | `ConcurrentOpenNeverExceedsSessionBudget`; máximo 4 sessões, uma invoke ativa/sessão, 4 pairings pendentes, 8 challenges, journal/request budgets finitos | IPC-01 confirma transporte real; flood fica em CI para não usar o aparelho como ferramenta de stress | CI + DEVICE positivo |
| IPC-17 | `InvalidOutputDoesNotBecomeSuccessAndSessionRecovers`; `HandlerErrorDoesNotLeakItsMessageAndSessionRecovers`; binding converte exceção em erro sanitizado | Nenhuma propriedade Android adicional | CI |
| IPC-18 | Integração é opcional; nenhum Product referencia código do provider; provider ausente retorna indisponível | Com provider ausente/forçado a parar, criar/abrir/editar/rodar projeto normalmente | Pendente DEVICE |

## Roteiro físico — somente quando os candidatos acima estiverem preenchidos

### A — Standalone / IPC-18

1. Instale apenas o caller candidato.
2. Abra o app, crie ou abra um projeto, edite e execute o fluxo normal.
3. Abra **Conexões** e toque **Conectar e testar**.
4. Esperado: informa provider indisponível; IDE/framework continuam funcionais.

### B — Primeiro pareamento / IPC-01, 02, 03, 07, 08 e 10

1. Instale o Hub candidato sem desinstalar o caller.
2. No caller, abra **Conexões**, mantenha o texto de teste e toque **Conectar e testar**.
3. Anote o código de 6 dígitos.
4. Abra Hub → **Conexões locais**.
5. Confirme que o código é exatamente o mesmo. Se diferir, **reprove e interrompa o gate**.
6. Aprove no Hub.
7. Volte ao caller e toque **Conectar e testar** de novo.
8. Esperado: “Conexão autenticada” e contagens coerentes; no Hub a conexão aparece como aprovada.

### C — Persistência, pin e revogação / IPC-04 e 13

1. Feche e reabra os dois aplicativos. Repita o ensaio; não deve pedir aprovação de novo.
2. Hub → **Conexões locais** → **Revogar**.
3. Caller → **Conectar e testar**.
4. Esperado: novo pareamento de 6 dígitos; confiança antiga não volta sozinha.
5. Aprove novamente e confirme funcionamento.
6. Caller → **Conexões** → **Esquecer conexão**.
7. Esperado: a chave da instalação do caller é girada; o próximo uso exige novo pareamento mesmo com package/signatário iguais.

### D — Lifecycle Binder / IPC-15

O candidato final deve oferecer uma janela de sessão observável **sem introduzir uma capability de produção artificial**. Se a sessão do ensaio fechar rápido demais para o teste, use apenas instrumentação/UI de observabilidade do binding, não altere `text.inspect`.

1. Abra uma sessão autenticada.
2. Force a parada do caller enquanto a sessão estiver aberta.
3. Esperado: o death-recipient fecha/cancela a sessão imediatamente; nenhum grant/sessão antiga reaparece ao reabrir.
4. Repita forçando parada do provider durante uma tentativa de conexão.
5. Esperado: caller mostra indisponibilidade/erro sanitizado e continua funcional fora da integração.

## Critério de fechamento

P5-4 só sai de `[~]` quando:

- Hub CI, Lunet CI/APK e consistency estiverem verdes no mesmo head candidato;
- cada IPC-01..18 estiver `CI`, `DEVICE pass` ou `N/A justificado`;
- UID/signatário/Keystore/Binder/pairing/process-death tiverem evidência Android real;
- o usuário aprovar a validação crítica pelo mecanismo canônico do repositório.

Até lá, PR #210 permanece draft/critical e sem label `integrar`.

## Resultado

Pendente.
