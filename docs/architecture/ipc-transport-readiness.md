# P5-4 — Comparação de transportes e prontidão

Escopo/IDs/estado: [ROADMAP P5-4](../../ROADMAP.md). Estado vivo: [Issue #194](https://github.com/AbnerCruz/Ecosystem/issues/194). Este documento é uma **avaliação preparatória**, não contrato de wire, ADR aceito, escolha de transporte ou prova de dois Hosts. Usa os critérios de [capability-runtime.md](capability-runtime.md) e ADR-0011/0012/0024. O contrato candidato de P5-3 está no PR #193; implementação de IPC só depois de sua integração e da decisão própria.

## Problema concreto a resolver

A mesma Tool deve operar sob o Hub e um Product Shell real, recebendo dados de contrato e Context/grants capturados pelo Host. Hospedar a Tool dentro de dois aplicativos **não exige**, por si só, fazer o Lunet chamar o Hub: o domínio essencial e a distribuição do Product continuam independentes dele (NN-003/023). O consumidor deve demonstrar uma operação que realmente cruza processos antes de justificar IPC. Chamada local pode ser referência semântica, mas nunca evidência de autenticação interprocessos.

## Prontidão observada (base da auditoria)

Auditoria do commit `da80358551e0e51ab713ab97fda070983530a6bf`.

| Consumidor | Evidência no código/fonte canônica | Lacuna para autorizar a primeira prova |
|---|---|---|
| Hub Android | `apps/hub/src/Hub.Android/Hub.Android.csproj`: `net10.0-android`, API mínima 29, `io.ecosystem.hub`; runtime local no Core | Ainda não há endpoint IPC exportado nem caller remoto autenticado |
| Lunet Android | `apps/lunet2d/src/Lunet.Android/Lunet.Android.csproj`: API mínima 29, `io.lunet.studio`; `ProjectManifest.GameId` persistido | Fase 3/DEVICE e governança local; falta sessão/grants/lifecycle do Host API, necessidade interprocessos demonstrada e suíte de consumidor |
| Urbe C# | `apps/urbe/docs/csharp/ROADMAP.md`: G-C0 aprovado; pilha WebAssembly PWA + MAUI Hybrid/RCL decidida | UC-8 aberto nesta base, Shell em UC-17 e Android em UC-25; JS congelado não recebe integração nova |
| Ecosystem AI | `apps/ecosystem-ai/src/AgentWorkspace/WorkspaceSession.cs`: sessão headless local | Sem segundo Product Shell visual pronto; prova em dois Contexts não é prova de dois aplicativos |

FATO OBSERVADO: nenhum candidato satisfaz todos os critérios de prontidão nesta base. **INFERÊNCIA:** Android é o caminho técnico inicial mais curto se o Lunet liberar sua integração. **PROPOSTA condicionada:** nessa ocasião avaliar primeiro serviço Android autenticado; isso não seleciona Binder nem autoriza exportar um Service agora. Urbe Web não é consumidor Binder; um binding Web futuro terá avaliação própria sem mudar a semântica da Host API.

## Comparação sem escolher prematuramente

| Alternativa | Autenticação do peer | Lifecycle/desconexão | Custo e limites | Adequação nesta etapa |
|---|---|---|---|---|
| Em processo | Identidade atribuída pelo Host confiável; não autentica outro aplicativo | Fechar sessão/cancelar via lifecycle do Shell | Menor custo; sem framing/wire; código confiável e cancelamento cooperativo | Referência da Host API; não satisfaz P5-4 |
| Bound Service Android sobre Binder | UID fornecido pelo Android na transação; resolver pacote/signatário e política aprovada; identidade capturada antes de async | Binding, morte do peer/processo, unbind; invalidar sessão explicitamente | Android apenas; marshalling/callbacks e concorrência; transações limitadas | Candidato para uma necessidade Android real; não exige servidor HTTP |
| Socket Unix local | Credenciais do peer do SO onde disponíveis; ainda requer mapear UID/pacote/signatário e política | EOF/erro, fechamento bilateral, deadline; protocolo de reconexão explícito | Endereço/namespace, acesso sob sandbox/SELinux, framing, buffers e canais por plataforma | Não presumir caminho compartilhado entre sandboxes Android; ensaio real antes de escolha |
| TCP loopback | Endereço local não prova identidade; pareamento/credencial e autenticação mútua próprios | EOF/erro, deadlines, sessões expiram; reconexão não restaura grants | Porta, disponibilidade, proteção de credenciais, parsing e backpressure; Web exige avaliar origem/políticas do navegador | Mais custo de confiança; não usar localhost como atalho de autenticação |

No Android, retornar uma classe Binder local e fazer cast no cliente só funciona no mesmo aplicativo/processo; IPC exige marshalling (por exemplo Messenger/AIDL ou transações explícitas). Não confundir o exemplo de LocalBinder com protocolo entre APKs. A documentação Android também informa que `onBind` pode entregar o mesmo Binder a múltiplos clientes; portanto identidade/concessão da sessão não pode depender de extras do Intent nem só da primeira chamada a `onBind`.

Para Binder, capturar UID no contexto da transação antes de delegar trabalho; o payload não escolhe caller/ator. O cliente também deve verificar a identidade do serviço alvo, e callbacks devem pertencer à sessão autenticada. Pacote esperado é parte do adapter/política de confiança, nunca algo enviado à Tool. Nome de pacote e UID isoladamente não substituem a política de signatário. Certificado de instalação confiável para P4-3 não concede automaticamente acesso runtime. Permissão Android `signature` só é candidata se a relação de assinatura de todos os participantes for verificada; não re-assinar Products por conveniência.

Para Unix, `LocalSocket.getPeerCredentials()` é um recurso Android documentado, não autorização para inventar um endpoint entre sandboxes. Para TCP, autenticação deve ser bilateral e vinculada à sessão/Context; não colocar token global em URL, log ou payload de Tool. Nenhum transporte utiliza GitHub como barramento runtime.

## Matriz de aceite a executar após decisão

Casos abaixo são **planejados, não executados**. Cada negativa deve provar que o handler não foi chamado e que não houve vazamento de conteúdo, grants ou capabilities proibidas. Sucesso exige apenas um resultado terminal, correlacionado à sessão/invocação correta. A binding decide os erros de transporte por ADR; não adicionar códigos ao contrato v1 por inferência.

| ID | Estímulo | Evidência exigida |
|---|---|---|
| IPC-01 | Peer autorizado, Context válido e capability permitida | Resultado conforme contrato e identidade capturada do peer real |
| IPC-02 | Pacote esperado com signatário não autorizado | Recusa antes de discovery/invoke; instalação não implica grant |
| IPC-03 | Pacote/UID/ator falsificados no payload | Identidade real prevalece; nenhum handler executado sob identidade falsa |
| IPC-04 | Cliente válido conecta a serviço falso | Cliente recusa identidade do provider antes de enviar input sensível |
| IPC-05 | Callback pertencente a outro peer/sessão | Recusa; resultado/progresso não redirecionados |
| IPC-06 | Peer usa ID de sessão/cancelamento de outro peer | Nenhuma leitura, execução, cancelamento ou revogação cruzada |
| IPC-07 | Capability fora do Context ou grant ausente | Discovery não revela; invoke não executa; erro não distingue a causa |
| IPC-08 | Payload tenta elevar grants ou trocar projeto | Host mantém scope/grants capturados e deny-by-default |
| IPC-09 | Protocolo/versão incompatível ou input malformado | Recusa sanitizada antes do handler; sessão/política não alteradas |
| IPC-10 | Frame incompleto, comprimento absurdo ou payload profundo | Limite aplicado antes da alocação excessiva; tempo/memória medidos |
| IPC-11 | Request duplicado/replay e reconexão | Política explícita por sessão; não executar command novamente silenciosamente |
| IPC-12 | Cancelamento durante execução e resultado tardio | Resultado/progresso tardios descartados; sessão utilizável conforme contrato |
| IPC-13 | Revogação durante execução | Grant removido antes de outra autorização; REVOKED prevalece; sem sucesso tardio |
| IPC-14 | Deadline expira com provider cooperativo e não cooperativo | Caller termina em tempo limitado; descartar tardios; documentar limite de isolamento |
| IPC-15 | Peer/processo morre, disconnect e unbind | Sessão invalidada, recursos liberados; sem reabrir com grants antigos |
| IPC-16 | Flood de sessões/requests/progresso | Budgets de fila, memória e taxa verificáveis; cancel não fica preso atrás do trabalho |
| IPC-17 | Provider lança ou produz output inválido | Erro sanitizado/contrato violado; nenhuma exceção/conteúdo privado em logs |
| IPC-18 | Hub ausente/offline/quebrado | Product continua instalável e funcional no domínio essencial; integração indisponível explícita |

O ensaio fixa antes valores de limite por frame/input/output, profundidade, chamadas/sessões concorrentes, fila e journal, duração e idle timeout. Não herdar limite de caracteres do `text.inspect` como limite de bytes do wire: UTF-8, cópias e marshalling diferem. Em Binder, o buffer de transações é compartilhado pelo processo; ficar abaixo de seu máximo nominal não prova ausência de `TransactionTooLargeException` sob concorrência. Limites precisam de ensaio real.

Deadline impede o caller de esperar indefinidamente, mas não prova interrupção física de um handler não cooperativo. Isolamento/encerramento de processo e política de supervisão exigem desenho explícito; não prometer preempção por `CancellationToken`. Cancelamento deve ter caminho de controle que não espere a fila de trabalho bloqueada.

## Saídas e ordem da implementação autorizável

1. Integrar P5-3 e verificar sua conformance no estado combinado; isso não seleciona transporte.
2. Reauditar o consumidor: liberação da governança local, operação interprocessos necessária, Context real e comportamento standalone.
3. Registrar ADR/decisão do transporte e da confiança, com comparação acima, ownership do endpoint, plataformas e política de caller/provider. Se não houver necessidade interprocessos, reavaliar o escopo em vez de inventar consumidor.
4. Implementar primeiro adapter/ensaio local no Product responsável; nenhum Product referencia `Hub.Core` e nenhuma Tool faz branching por Host.
5. Executar IPC-01..IPC-18 com processos distintos/identidade real e budgets fixados. Simulação/headless não substitui evidência interprocessos.
6. P5-5: mesma Tool em dois Hosts reais; Extraction Review positiva antes de mover runtime/Tool, respondendo NN-022. P5-7 permanece gate DEVICE separado.

Este slice não exige uma escolha do proprietário antes de existir o consumidor/ensaio concreto que fundamente a proposta. Não abre DEC prematura nem marca P5-4 concluído.

## Fontes primárias consultadas

- [Android: bound services](https://developer.android.com/develop/background-work/services/bound-services) — marshalling, LocalBinder, cache de onBind e lifecycle.
- [Android: Binder](https://developer.android.com/reference/android/os/Binder) — identidade de transação e chamadas remotas.
- [Android: LocalSocket](https://developer.android.com/reference/android/net/LocalSocket) — credenciais do peer; alcance limitado à API/plataforma.
- [Android: TransactionTooLargeException](https://developer.android.com/reference/android/os/TransactionTooLargeException) — buffer compartilhado e falhas sob concorrência.
- [Android: PackageManager](https://developer.android.com/reference/android/content/pm/PackageManager) — resolução e verificação de certificados; política runtime ainda depende de ADR próprio.

As fontes externas sustentam as propriedades das APIs; a recomendação de adequação é inferência técnica deste documento. As autoridades do Ecosystem continuam MANIFEST, decisões/ADRs e contratos canônicos.
