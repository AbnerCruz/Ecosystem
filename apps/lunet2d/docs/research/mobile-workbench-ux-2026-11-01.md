# LUNET-470 — Auditoria e redesign do workbench mobile (01/11/2026)

**Estado:** proposta implementada em branch isolada; CI, capturas reais e validação DEVICE pendentes. **Base da auditoria:** Ecosystem `main` `4e64b950b4424183dc045e5f3afcf9f2a604259b`. **Produto:** Lunet IDE Android, sem alteração de Framework, Runtime, Studio ou outros produtos. **Contrato:** SPEC §§9–13, ROADMAP e protocolo transversal.

## 1. Auditoria do código existente

| Evidência canônica | Diagnóstico verificável | Implicação |
| --- | --- | --- |
| `src/Lunet.Android/MainActivity.cs`, `ShowProjects` | Tela inicial usa sequência de botões básicos e lista vertical de botões; pouco agrupamento semântico, projetos sem informação contextual. | Falta hierarquia visual e clareza de ação primária. |
| `MainActivity.cs`, `ShowWorkspace` | Topo contém sete botões com peso relativo (voltar, explorer, run, desfazer, refazer, Assets, menu); um painel permanente de diagnósticos disputa altura do código. | Pressão horizontal e vertical, em especial ao abrir teclado. |
| `src/Lunet.Core/WorkspaceLayout.cs` | Painel aparece por padrão com fração de 30%, inclusive em retrato; `PanelDock.Auto` adapta lateral/baixo e os presets persistem. | Adaptação de dock já existe; corrigir default, não reescrever layout. |
| `MainActivity.Ide.cs`, `ShowMenu/ShowToolsMenu` | Ferramentas em múltiplas camadas de listas de `AlertDialog`; muitas ações não são localizáveis por busca. | Menor descobribilidade e mais toques para um comando. |
| `MainActivity.cs`, `BuildDrawer/ToggleExplorer` | Drawer cobre editor; sem scrim modal de indicação/fechamento externo. | Ausência de hierarquia de foco visual. |
| `src/Lunet.Android/Editor/CodeEditText*.cs` e `src/Lunet.Editor/**` | Editor e serviços (Roslyn, sugestões, edição) já são componentes independentes. | Não substituir editor nem introduzir dependência UI→Framework. |
| `MainActivity.Studio.cs` | Sprite e Tile Studio já são fluxos reais do projeto; têm UX própria. | Redesign do workbench não deve apagar nem copiar suas capacidades. |

**Limites da auditoria:** análise de fontes/contratos. Não houve instrumentação de performance, captura de tela real, experimento com participantes nem teste físico no momento da proposta. O relato de UI ruim é dado de usuário, não prova automática de causa.

## 2. Pesquisa externa: código e decisões

| Fonte estudada | Mecanismo e aprendizado útil | Decisão |
| --- | --- | --- |
| [VS Code / Quick Access, código](https://github.com/microsoft/vscode/blob/main/src/vs/workbench/browser/quickaccess.ts) e [User Interface](https://code.visualstudio.com/docs/editing/getting-started/userinterface) | Comandos pesquisáveis, separação do editor de painéis e preservação de estado ao abrir navegação transitória. | **Adaptar:** palette local do Lunet + foco principal no código, sem trazer Electron/Monaco. |
| [VS Code / layout](https://code.visualstudio.com/docs/configure/custom-layout) | Painéis reposicionáveis, layouts persistidos, alternativa de tabs únicas. | **Incorporar na camada existente:** `WorkspaceLayout` já implementa dock/presets; preservar, não duplicar. |
| [Acode / quickTools](https://github.com/Acode-Foundation/Acode/tree/main/src/components/quickTools), [tabView](https://github.com/Acode-Foundation/Acode/blob/main/src/components/tabView.js) | Ferramentas compactas ajustadas ao mobile e gestos em abas. | **Adaptar:** ações próximas do editor e tamanhos adequados, evitando copiar DOM ou swipe acidental no código. |
| [AndroidIDE / editor](https://github.com/AndroidIDEOfficial/AndroidIDE/tree/dev/editor) | Separação entre editor, ferramentas de IDE e projeto Android com interfaces de toque. | **Adaptar conceito:** manter backend C# e IME nativo; AndroidIDE original está arquivado desde 2024. |
| [Material Android / navegação](https://developer.android.com/design/ui/mobile/guides/layout-and-content/layout-and-nav-patterns) | Nav compacta de 3–5 destinos, drawer secundário, rail adaptativo quando há espaço. | **Adotar compact:** cinco destinos; **pendência:** rail e painel simultâneo em tablet. |
| [Android / tamanho de alvo](https://support.google.com/accessibility/android/answer/7101858) | Alvos de toque pelo menos 48×48 dp e espaçamento suficiente. | **Adotar:** controles primários com altura/área mínima de 48 dp. |

Nenhum componente de terceiros foi copiado ou incorporado. Comparação direta de arquivos e contratos, não apenas imagens de marketing.

## 3. Comunidades — aprovação, rejeição e desejo

- [Acode issue #2374](https://github.com/Acode-Foundation/Acode/issues/2374): relato de **múltiplos participantes** de teclado desaparecendo ao usar rápido desfazer/atalhos, gerando saltos do layout e toque acidental no código. Issue fechada com PR #2408. **Resposta:** botões da barra não devem capturar foco; teste DEVICE obrigatório com IME visível. Não inferir taxa de incidência.
- [Acode issue #2408](https://github.com/Acode-Foundation/Acode/pull/2408): correção comunitária aponta a preservação de foco como problema de UX, não mera questão estética. **Resposta:** não implementar toolbar que force perda de foco.
- [AndroidIDE repo e issues](https://github.com/AndroidIDEOfficial/AndroidIDE/issues): features valorizadas no produto incluem editor, console/log, Git, terminal e design de UI; o repositório original está arquivado. **Resposta:** ferramentas profissionais não devem desaparecer quando o editor vira área prioritária.
- Documentação oficial do VS Code e padrões Android reforçam a expectativa de descoberta de comandos, layouts preservados e navegação com hierarquia. Não se trata de survey próprio; necessidades são hipóteses sustentadas em mecanismos e relatos públicos.

## 4. Confronto e decisões

| Necessidade | Estado no Lunet | Ação | Critério |
| --- | --- | --- | --- |
| Espaço útil para código | Painel visível por padrão | **Incorporar:** código em foco por padrão | Painel oculto até pedido ou erro |
| Ações sem 4 menus aninhados | Listas hierárquicas | **Incorporar:** busca de comandos | Filtro sem distinção de caixa/acentos; qualquer ação selecionável |
| Rótulos previsíveis | Símbolos isolados / botões compactados | **Adaptar:** rótulos para principais e `ContentDescription` | Ação nomeada, alvo ≥48 dp |
| Navegação mobile | Drawer sem scrim + muitas barras | **Incorporar:** nav inferior, drawer com scrim e Back contextual | Navegação sem descarte do buffer |
| Teclado de código | EditText customizado em funcionamento | **Preservar:** não substituir a edição; bloquear roubo de foco na toolbar | Teclado permanece ao desfazer/refazer (verificar DEVICE) |
| IDE/Studio independentes | Boundaries existentes | **Preservar:** manter código/Studio e ação Run nativos | Sem dependências do Framework a UI |
| Editor desktop com rail e multi-janelas | Parcial | **Investigar / tarefa futura:** layout por tamanho da janela | Testes tablet/rotação e restauração, antes de adotar |
| Redesign integral de Sprite/Tile/Audio Studio | Fora do shell e sem testes UX reais | **Não afirmar feito:** auditar cada Studio separadamente | Design coerente, precisão de toque e undo preservado |

## 5. Novo fluxo e testes

1. Abrir Lunet → página de projetos com uma ação primária: **Novo projeto**, depois **Importar / Git / Ajustes** e lista de projetos.
2. Abrir projeto → nome na barra superior, **Run** destacado, arquivo atual, desfazer/refazer/buscar; código como área dominante.
3. Navegação compacta: **Código / Arquivos / Erros / Console / Ações**; Arquivos abre drawer sobre scrim; Back fecha drawer, depois painel, antes de sair do projeto.
4. **Ações** abre palette pesquisável em português com comandos de edição, execução, Git, assets e Studio; menus completos ainda disponíveis por compatibilidade.
5. Run com erro abre Erros; voltar de Preview mostra Console, sem descartar o arquivo; layouts previamente salvos continuam migráveis via JSON antigo.

**Testes TDD iniciais:** `tests/Lunet.Tests/MobileWorkbenchTests.cs` foi commitado **antes** do Core e da UI. Garante foco de código, transições do painel, rejeição de aba inválida e filtro de palette com acentuação. **Regressão:** `WorkspaceLayoutTests`, `EditorToolsTests`, `EndToEndTests` e CI completo. **Aparelho:** criar/abrir projeto, digitar com teclado aberto e acionar undo/redo repetidamente, drawer/scrim/Back, rotação retrato/paisagem, acessibilidade de botões, erro de compilação, Run/Stop, Studio, reabertura e retenção de layouts. Android Accessibility Scanner recomendado.

## 6. Riscos, lacunas e próximos gates

- `MainActivity.cs` ainda é uma Activity grande; redesign visual não é desculpa para reescrever compilador ou quebrar a persistência. Nova navegação foi isolada em `MainActivity.Workbench.cs`.
- **Ainda não há suporte adaptativo completo para tablet/desktop:** workspace com rail, rearranjo por largura efetiva da janela e múltiplos painéis exigem ensaio e trabalho posterior; nav inferior compacta é o corte presente.
- **Ainda não há redesign integral de todos os Studios:** este PR cobre launcher/workspace. Ferramentas internas têm dependências de toque, zoom e edição que requerem outra auditoria.
- A qualidade visual não foi provada com screenshots do aparelho, pesquisa com usuários, medição de carga, ou validação de TalkBack.
- Publicar APK apenas após CI do PR e integração governada. CI não substitui aprovação DEVICE do resultado.
