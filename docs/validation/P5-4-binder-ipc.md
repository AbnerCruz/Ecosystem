# P5-4 — Validação Android do IPC Binder autenticado

Estado: **candidato em construção; NÃO aprovar ainda**.

Este é o objeto da validação crítica do P5-4. A prova combina CI portátil, build real dos dois APKs e ensaio em aparelho com dois processos. Nenhum caso DEVICE é convertido em “passou” por inferência.

## Candidatos

- Hub: **a definir após o CI verde do head final do PR #210**.
- Caller Android: **a definir após o CI verde do head final do PR #210**.
- Commit testado: **a definir após estabilizar o PR #210**.
- Binding: `docs/contracts/bindings/android-binder-v1.md`.
- Trust policy: `docs/contracts/bindings/android-binder-v1.trust.json`.

## Matriz IPC-01..IPC-18

| ID | Evidência automatizada/código | Prova Android/DEVICE | Estado |
|---|---|---|---|
| IPC-01 | `AuthenticatedPeerCanOpenDiscoverInvokeAndClose`; Host API v1 + `text.inspect@1.0.0` | Parear Hub ↔ caller e obter contagens corretas por Binder em processos distintos | Pendente DEVICE |
| IPC-02 | Allowlist bilateral package+signer carregada do contrato; provider valida `Binder.CallingUid` → pacote/signatário antes de pairing | Negative harness/instalação com signer fora da allowlist deve ser recusada antes de pairing/discovery | Pendente DEVICE negativa |
| IPC-03 | Wire não possui campo de actor/UID/grants; sessão é vinculada ao peer do adapter; `SessionIdFromAnotherPeerNeverCrossesIdentityBoundary` | Tentar sessão/requestId adulterado no diagnóstico negativo sem alterar identidade efetiva | Parcial CI |
| IPC-04 | Caller filtra provider por package+signer e pinça a chave pública desde o código humano; challenge do provider é ECDSA | Troca/reinstalação do provider não pode enviar input sem novo pareamento explícito | Pendente DEVICE |
| IPC-05 | Binding v1 não expõe callback remoto/progress channel; não há superfície para redirecionar callback | Se callback/event wire for introduzido, este caso reabre antes do gate | N/A no wire v1 atual |
| IPC-06 | `SessionIdFromAnotherPeerNeverCrossesIdentityBoundary`; cancel/close também exigem peer proprietário | Negative harness com sessionId de outro peer | Parcial CI |
| IPC-07 | Host API conformance P5-3 + discovery filtrado pelo `LocalHostSession` | Discovery do caller mostra somente `text.inspect@1.0.0`; capability proibida não aparece | Pendente DEVICE |
| IPC-08 | Context só entra em `session.open`, fica capturado pela sessão; invoke/cancel não carregam Context/grants; grants são deny-by-default | Tentar alterar escopo após abrir sessão deve exigir nova sessão, nunca mutar a atual | Parcial CI |
| IPC-09 | `InvalidInputVersionAndProviderFailureRemainSanitized`; parser retorna erro sanitizado | Enviar versão/input inválidos pelo diagnóstico negativo | Parcial CI |
| IPC-10 | JSON <= 256 KiB; Parcel <= 640 KiB; provider verifica `DataAvail()` antes de decodificar string; Json depth permanece limitado pelo parser | Ensaio de payload limite/superior em processo real; observar ausência de crash/ANR/TransactionTooLarge | Pendente DEVICE |
| IPC-11 | `DuplicateRequestIsRejectedWithoutSecondProviderEffect`; reconnect abre sessão nova | Repetir requestId na mesma sessão e reconectar | Parcial CI |
| IPC-12 | `CancelUsesControlPathAndLateSuccessIsDiscarded`; sessão continua coerente | Cancelar operação diagnóstica longa em processo real; tardio não vira sucesso | Pendente DEVICE |
| IPC-13 | Revogação de grants da Host API já passa na conformance P5-3; `text.inspect` não exige permission grant. Pairing pode ser revogado explicitamente no Hub | Revogar conexão no Hub e provar que próxima sessão exige pareamento | Pendente DEVICE |
| IPC-14 | `NonCooperativeProviderCannotHoldCallerPastDeadline`; deadline padrão 10 s, máximo 30 s | Diagnóstico longo não cooperativo deve liberar caller no deadline; sessão fica busy até trabalho tardio acabar | Parcial CI / DEVICE |
| IPC-15 | Death token Binder em `session.open` + `LinkToDeath`; `Disconnect` cancela sessões; idle 2 min como fallback | Matar processo do caller durante sessão e provar invalidação imediata; matar provider produz indisponível sem quebrar caller | Pendente DEVICE |
| IPC-16 | `ConcurrentOpenNeverExceedsSessionBudget`; máximo 4 sessões, 1 invoke ativa/sessão; pending pairings 4; challenges 8; request journal finito | Flood controlado de sessões/frames e confirmação de UI responsiva/cancel funcional | Parcial CI / DEVICE |
| IPC-17 | `InvalidInputVersionAndProviderFailureRemainSanitized` + validação de output do Host API | Provider failure/invalid output no diagnóstico não pode expor exceção, path ou texto privado | Parcial CI |
| IPC-18 | Integração é opcional; botão Conexões retorna provider indisponível; nenhum Product referencia código do provider | Com provider ausente/forçado a parar, criar/abrir/editar/rodar projeto normalmente | Pendente DEVICE |

## Roteiro físico — executar somente com candidatos preenchidos

### A — Standalone

1. Instale apenas o caller candidato.
2. Abra o app, crie/abra um projeto, edite e execute o fluxo normal.
3. Abra **Conexões** e tente o ensaio.
4. Esperado: integração informa provider indisponível; IDE/framework continuam funcionais. Isso cobre IPC-18 e NN-003/023.

### B — Primeiro pareamento

1. Instale o Hub candidato sem desinstalar o caller.
2. No caller, abra **Conexões**, mantenha um texto simples e toque **Conectar e testar**.
3. Anote o código de 6 dígitos mostrado.
4. Abra o Hub → **Conexões locais**.
5. Confirme que o código é exatamente o mesmo. Se diferir, **reprove e pare o teste**.
6. Aprove no Hub.
7. Volte ao caller e toque **Conectar e testar** novamente.
8. Esperado: “Conexão autenticada” e contagens coerentes de caracteres/palavras/linhas.

### C — Persistência, pin e revogação

1. Feche/reabra os dois apps e repita o ensaio; não deve pedir aprovação novamente enquanto package/signer/chaves forem os mesmos.
2. No Hub → **Conexões locais** → **Revogar**.
3. No caller, tente novamente.
4. Esperado: surge um novo pareamento; conexão antiga não é restaurada automaticamente.
5. No caller, use **Esquecer conexão** e confirme que o próximo uso também exige pareamento.

### D — Lifecycle real

1. Inicie um ensaio de diagnóstico longo quando o build expuser esse modo.
2. Mate o processo do caller durante a sessão.
3. Esperado: death-recipient invalida/cancela a sessão no provider; nenhum sucesso tardio é publicado.
4. Repita matando/forçando parada do provider.
5. Esperado: caller termina com indisponibilidade/erro sanitizado e continua funcional fora da integração.

### E — Limites e negativas

Executar os modos diagnósticos do candidato para: request duplicado, sessionId inválido, assinatura/challenge inválidos, versão/input inválidos, frame acima do limite, deadline/cancel e flood limitado. Os resultados devem corresponder à matriz acima e não podem exibir stack trace, path privado, chave privada ou conteúdo do input em logs.

## Critério de fechamento

P5-4 só pode sair de `[~]` quando:

- Hub CI, Lunet CI/APK e consistency estiverem verdes no mesmo head candidato;
- IPC-01..18 estiverem `pass`, `N/A justificado` ou explicitamente cobertos por prova canônica anterior que o binding não altera;
- casos que dependem de UID/signatário/Keystore/Binder/process death tiverem evidência Android real;
- o usuário aprovar a validação crítica pelo mecanismo canônico do repositório.

Até lá, PR #210 permanece draft/critical e sem label `integrar`.

## Resultado

Pendente.
