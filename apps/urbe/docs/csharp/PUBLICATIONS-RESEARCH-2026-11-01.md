# Urbe — Auditoria confrontada do Editor e de Publicações (01/11/2026)

> **Escopo e autoridade:** investigação aplicada à `main` em `703d99ccfe1f4c56a6dc71e7c57300d90c0891ab`, complementada pela consolidação do proprietário [Numa 2.5](../../../../docs/product-plans/urbe/Numa_Consolidacao_2.5.md). Este documento registra **evidências e decisões locais de execução**, não é um segundo roadmap; IDs CST permanecem em aberto até testes, CI e DEVICE adequados. Protocolo: `docs/governance/protocolo-estudo-desenvolvimento.md`.

## 1. Auditoria interna — código real

| Caso | Código visto | Estado/risco |
|---|---|---|
| Troca direta de notas | `Urbe.Client/EditorOverlay.cs`: `Open` chamava `Show` sem `LeaveCurrentAsync`, diferentemente de `NavigateAsync` e `BackAsync`. `MainView.OpenNoteByPath` e `OpenFromNotes` chamam `Editor.Open`. | Risco estrutural reproduzível por estado: a troca sobrescreve `Editor.Text` antes de persistir rascunho. Corrigido nesta branch, testes adicionados; **não validado em CI/DEVICE**. CST-144 parcial. |
| Colisão de controles | `Urbe.Client/EditorOverlay.cs` `Footer`: `_readChip` ancorado à direita. `Urbe.Client/Ui/RadialNav.cs`: trigger inferior direito de 56 dp + 16 dp e ZIndex 20 em `MainView.Assistant.cs`. | Controles compartilham o canto; reposicionado chip para a esquerda nas larguras compactas, com teste geométrico. **Não implica CST-143 concluído** sem verificação visual/toque. |
| Persistência de notas | `EditorOverlay.SaveSnapshotAsync/SaveNowAsync`; `MainView.SaveNoteAsync` e `IUrbeVaultStorage`; `DocumentStore.Upsert` em `DocumentModel.cs`. | Há persistência verificada como fluxo, autosave e recuperação em memória de falhas; inspeção NÃO comprova resistência a morte de processo, IME e cenários externos. CST-004–007/016/129/144 permanecem abertos. |
| Páginas legadas | `Urbe.Client/PageStudioView*.cs`, `MainView.Pages.cs`, `MainView.Studio.cs`, `Urbe.Core/Pages/PageEngine.*`, `AssistantPageTools.cs`, `PageModel.cs`. | Estúdio antigo ainda presente na main, criando `.page.json`. Decisão do proprietário descontinua a UI, **não os arquivos**. Remoção só após novo fluxo verificado e inventário completo de chamadas. CST-149/157 pendentes. |
| Identidade real do vault | `DocumentModel.cs` expõe `UrbeDocument.Id`, `Revision`, `DocumentStore.Get(id)`, `Changed` e `Upsert`, inclusive após renomeio com mesmo ID. `VaultIdentity.cs` trabalha com IDs persistidos. | Pode sustentar referência viva; **não** reusar `EditorReferenceSession` como fonte de verdade porque ele guarda snapshots transitórios de Markdown. |
| Contrato inicial de Publicações | `Urbe.Core/PublicationSources.cs` (nesta branch). | `PublicationSourceCatalog` recebe `DocumentStore`, resolve texto somente **sob demanda por ID**, emite invalidações e declara seletores de bloco ainda não suportados. Nenhuma segunda cópia autoral, nenhum writer paralelo. CST-151/153 **parciais**. |

## 2. Estado da arte — referências abertas e confrontadas

1. **ProseMirror — guia oficial e código de entrada:** https://prosemirror.net/docs/guide/ e https://github.com/ProseMirror/prosemirror-view/blob/master/src/input.ts. A arquitetura distingue modelo, view, estado e transformações; histórico depende de operações registráveis, não de refazer árvore visual a cada toque. O código `input.ts` acompanha explicitamente estados de composição IME e eventos de input, com atenção a Android. **Adaptação ao Urbe:** preservar origem e posição da edição, separar entrada (IME) de regeneração visual e tornar escrita/revisão verificáveis. **Rejeição:** portar ProseMirror/WebView como substituto do editor nativo só para resolver foco/teclado (não foi demonstrada necessidade, acrescentaria outro editor e acoplamento).
2. **Obsidian — incorporações oficiais:** https://obsidian.md/help/embeds — notas e trechos podem ser referidos por heading/bloco, e a visualização reflete a origem. **Adaptação:** publicações devem usar a identidade real `UrbeDocument.Id`, não o caminho ou texto colado; o contrato de fragmento depende de ID semântico persistente no Urbe. **Não implementar** offsets frágeis nem prometer edições bidirecionais antes de proteção de conflito.
3. **JSON Canvas — especificação aberta:** https://jsoncanvas.org/ — nós `type: file` referenciam caminhos de arquivos separados das coordenadas/layout. **Adaptação:** separar conteúdo real de composição. **Limite:** caminho não é identidade suficiente para mover/renomear, e o formato de quadro não substitui esquema multiformato de Publicações.
4. **Sync Embeds (Obsidian):** https://community.obsidian.md/plugins/sync-embeds. O fluxo edita a nota-fonte em um editor real embutido; separa layout do texto-fonte. **Adaptação:** um único writer/undo/histórico/autoria pelo contrato nativo do Urbe; **rejeição:** copiar a técnica de reparenting de uma view JavaScript (não se aplica automaticamente ao Avalonia).
5. **Quarto Books:** https://quarto.org/docs/books/ — capítulos de fontes separadas, renderizados em HTML/PDF/Word/EPUB e outros destinos. **Adaptação:** árvore semântica + adaptadores de destino verificáveis, nunca converter HTML renderizado de volta para PPTX editável. **Limite:** pipeline de linha de comando não é a UX touch-first requerida.
6. **Obsidian Canvas (oficial):** https://obsidian.md/help/plugins/canvas. Confirma organização visual de notas e ativos referidos por arquivo. **Limite:** não substituir um produto de autoria editorial multimodal por canvas genérico.

## 3. Comunidades — aprovação, rejeição e desejos sem inferência de prevalência

- **Desejo / rejeição de fluxo atual:** https://www.reddit.com/r/ObsidianMD/comments/1qw13bu/frustrations_with_embedding_feature/ (relato individual, 04/02/2026): deseja edição do conteúdo incorporado sem navegação e rejeita aparência/padding intrusivos. Evidência *qualitativa*, não prevalência. Confirma que prévia sem edição de fonte não atende CST-152.
- **Rejeição / risco IME:** https://www.reddit.com/r/ObsidianMD/comments/1x8sfey/autocorrect_keyboard_bug_in_android/ (18–20/10/2026): relatos de Gboard, teclado Samsung e HeliBoard fechando quando cursor entra em palavras com sugestões. Não comprova causa ou defeito do Urbe; gera casos de teste do CST-016 com e sem corretor.
- **Confirmação de relevância de navegação e foco:** https://community.logos.com/forums/topic/238483-keyboard-bug-after-update-to-v53/ (set/2026) descreve comportamento distinto do teclado conforme origem de abertura da nota; o padrão sugere testar entrada via links versus diretamente via Explorer no Urbe, sem transferir diagnóstico entre apps.
- **Aprovação de fluxo em produto de referência:** https://community.obsidian.md/plugins/sync-embeds oferece edição in-place e atalhos; o reconhecimento de usuários mostrado na página é dado do plugin, não métrica extrapolável ao Urbe.

## 4. Confronto e decisões

| Descoberta | Confronto com Urbe | Decisão | Tarefa e critério |
|---|---|---|---|
| Estado de edição precisa sobreviver a navegação | `Open` não persistia texto antes de `Show`; `NavigateAsync` persistia. | **Incorporar já**, reaproveitando `LeaveCurrentAsync`; não recriar sistema de save. | CST-144: testes de troca direta/mesma nota; CI, falhas de IO, interrupção e teste físico ainda necessários. |
| Navegação flutuante não deve impedir edição | Radial cobre canto direito do footer. | **Adaptar** posição do switch em largura compacta, preservar desktop. | CST-143: teste geometria em 320/360/412/1100 dp e teste touch/IME físico. |
| Publicação exige fonte viva | `DocumentStore` fornece Id/Revision/Changed; `EditorReferenceSession` é snapshot. | **Incorporar** read catalog id-based; não persistir Markdown no manifest. | CST-150–153: faltam fragmentos estáveis, armazenamento do projeto, UI, edição transacional, dependências/exportação. |
| Páginas legado tem render e fluxo próprio | Muitas referências UI/ferramentas no app; arquivos da pessoa podem existir. | **Substituir sem apagar dados**; não remover até novo entrypoint confiável. | CST-149/157/158: inventário completo e transformação explícita não destrutiva. |
| Publicação multiformato | `PageEngine` e impressão Android são orientados a HTML/PDF legado. | **Investigar e adaptar** renderizadores úteis sob modelo semântico novo; não tratar print-dialog como exportação validada. | CST-156,164+, DEVICE real. |

## 5. Evidências / impedimentos conhecidos

- Documentos editados nesta branch; não houve `dotnet test`, build de APK nem teste físico nesta sessão. `.NET SDK` e Android SDK não estão disponíveis no executor local; conferir **Actions do PR** para o resultado automatizado real.
- Não marcar CST-142/143/144, 149–153 como concluídos. Testes de UI podem falhar por detalhes de layout e precisam de correção guiada pelo CI.
- Primeiro gate de integração: verificação automática dos testes `RadialNavUiTests`, `EditorNavigationUiTests`, `PublicationSourceCatalogTests` e da suíte afetada. O proprietário não deve ser requisitado para aprovar mudanças rotineiras; DEVICE fica explicitamente pendente.
