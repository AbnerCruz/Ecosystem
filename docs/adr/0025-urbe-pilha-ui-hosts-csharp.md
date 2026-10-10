# ADR-0025 — Pilha de UI e hosts do Urbe em C#

## Status

Substituído — na parte de UI e hosts, por [ADR-0032](0032-urbe-ui-nativa-sem-webview.md) (DEC-0043, 2026-10-10): o proprietário retirou o Web/PWA (09/10) e a tecnologia web da interface (10/10). Histórico: **DEC-0035-A** escolhida pelo proprietário em 2026-10-04. A fronteira `Urbe.Core` C# puro continua válida.

## Contexto

DEC-0024-B determinou uma reescrita completa do Urbe em C#, mantendo o cliente JavaScript distribuído até paridade total. UC-1..UC-4 e UC-7 estão concluídos; UC-2 fornece 297 casos portáveis, os 14 E2E funcionais congelados e protocolo físico posterior. UC-4 inventariou as dependências sem equivalente automático: editor/DOM, File System Access, IndexedDB, KaTeX, PDF, IA, plugins, Electron e Capacitor.

As três superfícies continuam obrigatórias:

| Superfície | Obrigação |
|---|---|
| Web/PWA | estático, local-first/offline, preservação do armazenamento por origem, funcionamento em navegador e instalação PWA |
| Windows | aplicativo instalável, filesystem real, picker, watcher, impressão/PDF, atualização e vault existente |
| Android | APK atualizável, mesmo domínio do produto, storage/permissões, back/minimize, exportação, impressão e toque/teclado virtual |

A aplicação é fortemente orientada a texto. O editor visual, seleção, IME, teclado virtual, paste, links e acessibilidade são tão importantes quanto o mapa 2D. REQ-107 exige comportamento específico junto ao teclado virtual. Por isso, “um framework desenha pixels nas três plataformas” não basta como critério.

### Fatos atuais sobre as opções

- O .NET 10 oferece Blazor WebAssembly PWA com suporte offline em build publicado. A Microsoft também documenta compartilhamento de Razor Components por Razor Class Library entre Web e .NET MAUI Blazor Hybrid; no Hybrid os componentes executam em .NET no dispositivo e renderizam em WebView, com acesso a capacidades nativas por serviços .NET.
- Avalonia 12 declara Windows, Android e WebAssembly com uma UI C#/XAML compartilhada. A própria documentação registra IME **parcial** no alvo WebAssembly, risco material para um editor como o Urbe.
- Uno Platform declara Windows, Android e WebAssembly com C#/XAML e um único código de UI.
- Nenhuma dessas declarações externas prova paridade do Urbe. UC-17..UC-25 e UC-29 continuam obrigatórios.

“C# total” neste programa significa que domínio, estado, regras, fluxo, agentes, editor e lógica de produto pertencem ao cliente C#. Não significa fingir que um navegador deixa de usar service worker, bootstrap WASM ou interoperabilidade com APIs Web. Qualquer JavaScript escrito especificamente pelo Urbe fica restrito a um adapter mínimo quando não houver API .NET equivalente, sem autoridade de domínio ou estado canônico.

## Problema

Escolher uma pilha que minimize duplicação entre Web/PWA, Windows e Android sem sacrificar o editor mobile-first, o funcionamento offline, o acesso a capacidades nativas e a possibilidade de provar os 297 casos de UC-2.

## Opções

| Opção | Desenho | Pontos fortes | Riscos/custo |
|---|---|---|---|
| **A — Blazor WebAssembly PWA + .NET MAUI Blazor Hybrid + RCL compartilhada** **(recomendada)** | `Urbe.Core` C# puro; componentes Razor compartilhados em RCL; host Web standalone Blazor WASM PWA; host MAUI Hybrid para Windows/Android; adapters de plataforma atrás de interfaces | Caminho oficial Microsoft para reaproveitar UI Web/native; DOM real para editor, IME e acessibilidade; PWA estática/offline; hosts nativos mantêm acesso .NET a filesystem, picker, back, impressão e update | WebView nos hosts nativos; CSS/DOM continuam parte da UI; browser exige service worker/bootstrap e possivelmente interop mínima; mapa 2D precisa de prova de performance própria |
| **B — Avalonia 12 em Windows/Android/WebAssembly** | Um projeto C#/XAML com heads por plataforma e renderer Avalonia | Uma UI C#/XAML, forte desenho 2D, menos divergência visual e sem WebView no desenho principal | IME WebAssembly oficialmente parcial; editor rico/contenteditable deixa de aproveitar o DOM e vira risco maior; browser APIs ainda exigem adapters/interoperabilidade; precisa provar PWA/offline e acessibilidade |
| **C — Uno Platform em Windows/Android/WebAssembly** | C#/XAML único, targets nativos e WASM | Alta reutilização, controles/WinUI, WebAssembly e mobile/desktop numa pilha | Nova dependência estrutural relevante; editor, PWA/offline, storage legado e performance do mundo precisam de spikes; menor aderência direta ao comportamento DOM já congelado |
| **D — UI separada por superfície, Core compartilhado** | Blazor WASM no Web; UI nativa C# distinta em Windows/Android; apenas domínio/serviços compartilhados | Máxima liberdade e fidelidade por host | Duplica UI, testes e correções; aumenta brutalmente o custo de paridade de editor/mundo e o risco de três Urbes diferentes |

## Decisão

**DEC-0035-A aceita:** Blazor WebAssembly PWA no Web + .NET MAUI Blazor Hybrid em Windows/Android, com UI compartilhada em Razor Class Library.

A alternativa **A** foi escolhida pelo proprietário. O Urbe é mais parecido com um ambiente de conhecimento/editor que também possui um mundo 2D do que com um jogo que por acaso edita texto. Preservar semântica web para o editor reduz o risco de IME, seleção, teclado móvel e acessibilidade; compartilhar os mesmos Razor Components entre Web e hosts nativos evita a duplicação da opção D. O mundo 2D continua podendo usar uma superfície de desenho encapsulada pelo componente, com implementação validada por benchmark e E2E antes de G-C3.

Se A for escolhida, a fronteira proposta é:

```text
Urbe.Core                C# puro: vault, domínio, conhecimento, mundo, markdown/serialização
Urbe.UI                  Razor Class Library: shell, editor, explorer, páginas, mundo
Urbe.Web                 Blazor WebAssembly PWA estático
Urbe.App                 .NET MAUI Blazor Hybrid
  ├─ Windows             adapters nativos
  └─ Android             adapters nativos
Urbe.Platform.Contracts  interfaces; nenhum Host concreto conhecido pelo Core/UI
```

Isto é desenho de responsabilidade, não autorização para extrair componentes ao Ecosystem. Tudo nasce local em `apps/urbe` (NN-022).

## Consequências

- UC-8 só cria o esqueleto correspondente **depois** da decisão e de G-C0.
- A UI compartilhada não pode chamar MAUI/Android/Windows diretamente; capacidades de plataforma entram por interfaces/adapters.
- O host Web deve continuar publicável como arquivos estáticos e funcionar offline depois do primeiro carregamento publicado.
- O browser deve manter acesso/migração do IndexedDB existente; escolher outro storage não é consequência automática da pilha.
- O renderer do mundo e o editor precisam de testes dedicados em Android real antes de G-C3. “Compila nas três plataformas” não é aceite.
- JavaScript específico do Urbe, se inevitável no Web, é adapter estreito, testado e sem regra de domínio; bibliotecas/bootstraps do framework não viram fonte canônica.
- KaTeX, PDF e plugins continuam decisões próprias de UC-16/UC-20; esta ADR não escolhe substitutos por acidente.

## Alternativas rejeitadas

Rejeitadas por DEC-0035: B (Avalonia 12), C (Uno Platform) e D (UI separada por superfície). Permanecem documentadas acima como histórico de tradeoffs.

## Referências

- DEC-0024-B; [ADR-0016](0016-migracao-do-urbe-para-csharp.md); Urbe ADR-0010.
- `apps/urbe/docs/csharp/DEPENDENCIES.md`, `PARITY.md`, `acceptance/surface-protocol.json`.
- MANIFEST NN-002, NN-005, NN-011, NN-017, NN-020, NN-022, NN-023.
- Microsoft Learn: https://learn.microsoft.com/aspnet/core/blazor/progressive-web-app/?view=aspnetcore-10.0
- Microsoft Learn: https://learn.microsoft.com/aspnet/core/blazor/hybrid/?view=aspnetcore-10.0
- Microsoft Learn: https://learn.microsoft.com/aspnet/core/blazor/hybrid/tutorials/maui-blazor-web-app?view=aspnetcore-10.0
- Avalonia 12: https://docs.avaloniaui.net/ and https://docs.avaloniaui.net/docs/input-interaction/text-input
- Uno Platform: https://platform.uno/platforms/
