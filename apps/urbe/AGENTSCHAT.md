### 2026-10-07 — ChatGPT — UC-18 terceira fatia: Explorer operacional

- Base: `fab94d76` após integração das duas primeiras fatias de UC-18.
- Implementado: criação de pastas vazias na sessão, enumeração de pastas, fila host-neutra de mutações `CreateFolder/Move`, movimento de arquivos e árvores de pasta preservando IDs e documentos abertos.
- Explorer: seleção de pasta expõe ações; desktop usa drag-and-drop; toque usa long press para armar movimento e toque na pasta para concluir; local atual também aceita drop/movimento.
- Segurança de dados: esta fatia NÃO escreve no filesystem; operações ficam em `PendingMutations` para o adapter canônico do host persistir depois. Nenhuma autoridade paralela de IO foi criada.
- Smoke Web ampliado para criar pasta, mover por mouse, armar por long press/touch, verificar destino e manter Editor/abas/undo/redo funcionando.
- Ainda falta em UC-18: edição Visual direta, toolbar/visualViewport, persistência física por host e requisitos avançados (comentários/painel/templates/timer/estado da toolbar).

### 2026-10-07 — ChatGPT — UC-18 / sessão de edição 2

- Estado: segunda fatia preparada sobre a primeira fatia já integrada em `9efb5555`.
- Feito: abas reais do editor com ativação/fechamento, limite de 12 abas e suporte interno a pin; histórico por documento com até 100 estados e `Desfazer`/`Refazer`; reset seguro ao trocar de vault; wikilinks não resolvidos aparecem como ação explícita para criar a nota na pasta atual e abri-la.
- Teste: smoke Web edita duas versões em Fonte, desfaz/refaz, verifica render Visual, cria nota a partir de wikilink inexistente, volta pela pilha correta, abre segunda nota e troca entre abas.
- Decisões: segue a semântica observada de `src/editor/session.js`; nenhuma nova autoridade de persistência, nenhum JS de produto e nenhuma mudança de formato.
- Pendências: edição Visual direta, persistência física dos hosts, split view e operações/gestos completos do Explorer continuam dentro da UC-18.
- Próximos passos: integrar esta fatia se rotina e então portar edição Visual interativa sem segunda autoridade Markdown.

### 2026-10-07 — ChatGPT — UC-18 / Issue #281

- Estado: primeira fatia implementada; aguardando CI e integração.
- Feito: sessão compartilhada `WorkspaceSession` em `Urbe.UI`; carregamento de `VaultSnapshot`; Explorer por pasta e busca, classificação por `ArtifactModel`, criação de notas e abertura; Editor com pilha de origem, modo Fonte editável, render Visual via `MarkdownEngine`, conversão Visual→Markdown via `VisualMarkdown`, links/backlinks via `KnowledgeIndex`; sessão registrada igualmente em Web e MAUI.
- Testes: o smoke Web real agora percorre Explorer → criar nota → Editor Fonte → render Visual → Voltar, em `/` e `/preview/`, online/offline. O projeto de teste separado foi descartado para não transformar uma fatia rotineira em mudança crítica só por tocar `.csproj`.
- Limite desta fatia: o modo Visual ainda é renderização, não edição DOM direta; hosts ainda não conectam armazenamento físico à sessão. Esses dois pontos permanecem dentro da UC-18 e não são declarados concluídos.
- Decisões: nenhuma nova decisão estrutural; usa somente autoridades C# já aprovadas em M1/M2.
- Próximos passos: CI do PR; depois conectar edição Visual interativa sem segunda autoridade de Markdown e integrar o carregamento/persistência do workspace por adapters de host.

### 2026-10-07 — ChatGPT — UC-17 shell e navegação

- UC-16/G-C2 foi autorizado e integrado em `c3ec27e6`; Issue #257 encerrada e UC-17 aberta como Issue #279.
- Criado shell compartilhado em `Urbe.UI`, usado igualmente por Web e MAUI Hybrid.
- Navegação canônica: Início, Explorer, Editor, Cidade e Mais; superfícies posteriores permanecem placeholders explícitos com UC responsável.
- Layout mobile-first com safe-area, barra inferior no celular e rail lateral em viewport maior; nenhum corte do cliente distribuído.
- `web-smoke.mjs` agora prova rotas ativas, base path `/` e `/preview/`, recarga com service worker, navegação offline e ausência de overflow horizontal.
- Esta fatia não declara G-C3 nem validação humana de layout/toque; isso continua reservado aos gates posteriores.

### 2026-10-07 — ChatGPT — UC-16 / G-C2 closeout

- Todas as fatias matemáticas anteriores estão integradas na main até `680e6db6`.
- Adicionada suíte final `MathDomainAcceptanceTests` cruzando edição, compatibilidade, macros, renderer, corpus real 30/31, determinismo e geometria SVG.
- ADR-0012 passa de Proposed para Accepted porque sua condição explícita foi satisfeita: PR crítico #274 autorizado e integrado.
- ROADMAP fecha UC-16 e aprova G-C2 somente para domínio sem UI; aparência/toque continuam G-C3 e não são inferidos.
- Próxima tarefa passa a UC-17 — shell e navegação.
- Closeout toca ADR e é declarado crítico por constituição; integração deve respeitar o gate normal do Ecosystem.

### 2026-10-06 — ChatGPT — UC-16 macros de renderização sem estado global

- Auditoria do legado confirmou `UrbeMath.setMacros(m)`; não há consumidor/teste interno, mas a investigação de UC-16 exige camada limitada de macros antes de G-C2.
- Portado `MathMacros` para dicionários de string explícitos por renderização, com #1..#9, grupos/tokens, macros aninhadas e preservação do TeX original.
- Segurança/determinismo: sem mutação global de CSharpMath, sem `newcommand/def`, com limites de definições, expansão, profundidade e detecção de ciclo.
- `MathRenderer.RenderSvg(..., macros: ...)` usa a expansão apenas internamente; resultado e acessibilidade continuam referindo o TeX do usuário.
- Branch empilhada sobre #276; só será retargetada para main depois da fatia de diagnósticos integrar.

### 2026-10-06 — ChatGPT — UC-16 renderer consome diagnósticos estruturados

- Branch empilhada sobre #275 para não bloquear a integração da camada pura de compatibilidade.
- `MathRenderResult` mantém o construtor existente e ganha `CompatibilityDiagnostics`.
- `MathRenderer` faz preflight dos comandos/macros conhecidos antes de CSharpMath, preservando TeX e distinguindo incompatibilidade conhecida de erro genérico do parser.
- Testes cobrem `boxed`, `newcommand`, comando desconhecido, limites, determinismo e corpus real 30/31.
- Sem nova dependência, ADR, UI, IO ou dado persistido; a fatia é rotineira e será retargetada para main após #275 integrar.

### 2026-10-06 — ChatGPT — UC-16 compatibilidade TeX independente do renderer

- PR #274 está verde e aguardando autorização crítica para a dependência/ADR; esta fatia não toca esse gate.
- Escopo independente: analisador puro de compatibilidade TeX/macros, sem PackageReference, IO, UI ou mutação do texto.
- Diagnósticos cobrem incompatibilidades observadas no probe #265 e declarações dinâmicas de macro; offsets seguem UTF-16 do editor.
- Regra: nenhuma substituição automática (dfrac/frac, boldsymbol/mathbf, normas etc. não são equivalentes gerais).
- Próximo: testar esta fatia como PR rotineiro; depois consumir os diagnósticos no renderer somente se/como o PR crítico for autorizado.

### 2026-10-06 — ChatGPT — UC-16 renderer C# / SVG próprio

- Escolha proposta após o probe #265: `CSharpMath.Rendering 1.0.0-pre.2` + backend SVG do Urbe; VectSharp/LGPL fica fora.
- Pacotes CSharpMath/Rendering fixados exatamente em `1.0.0-pre.2`; alteração de csproj torna o PR crítico por política.
- `MathRenderer` preserva TeX, limita entrada/fonte e devolve diagnóstico em vez de exceção de host.
- `SvgMathCanvas` implementa `ICanvas/Path` e emite somente geometria SVG autocontida, sem links/scripts/texto externo.
- Testes cobrem Bhaskara, integral, matriz, determinismo, entradas inválidas e o corpus real do tutorial com piso de 30/31.
- ADR-0012 fica Proposed até autorização do proprietário; macros/aliases incompatíveis continuam explícitos, sem reescrita silenciosa.

### 2026-10-06 — ChatGPT — UC-15 concluída

- Release 1.8.4-beta publicada separadamente; fechamento de UC-15 não reutiliza evidência DEVICE da release.
- Auditoria final da Issue #249 confirmou cobertura do domínio puro exigido: páginas/composições, proteção forward/REQ-049, blocos, livro, layout livre, conversão/compactação, temas, shell HTML e templates.
- Merge final #270 = `df66859924bb508f2320da9c676a0dc4e669530c`.
- Main verde: csharp-portable, checks/Web, Android, Windows, E2E e Foundation consistency (runs `37517474704` / `37517474691`).
- UI/Studio permanece UC-18; KaTeX/render matemática permanece UC-16; IA page_schema/write_page permanece UC-21; nenhuma escrita crítica de migração foi introduzida.
- Próxima tarefa do M2: UC-16.

### 2026-10-06 — ChatGPT — UC-15 sexta fatia: shell, temas e templates

- PR #269 integrou a quinta fatia na main (`c7bf1ba4`) com Core/Web/Android/Windows/E2E/consistency verdes.
- Auditoria contra `tests/pages.mjs` encontrou uma lacuna real: o C# renderizava blocos, mas ainda ignorava tema/layout global e não tinha o catálogo puro de templates.
- Portados os 10 presets de tema/fontes, validação de tema/estilo de seção, shell HTML com nav/rodapé/metadados e CSS de livro.
- Portados os 13 templates embutidos e variantes `simple`/`skeleton`, sem Studio/DOM/IO.
- Testes novos exercitam todos os presets/templates, validação forward-safe, HTML completo e CSS de impressão.
- UC-15 permanece aberta até esta fatia integrar e a auditoria final confirmar que só restam responsabilidades explicitamente fora do escopo.

### 2026-10-06 — ChatGPT — UC-15 quinta fatia: conversão e compactação

- PR #254 integrou o layout livre C# na main (`b1ce8347`); a nova branch parte desse estado.
- Portado o conversor dos 13 blocos fechados declarados por `pages/free.js`, incluindo Markdown→peças e resolução de capítulo/nota via `DocumentStore`.
- Adicionada compactação de defaults com round-trip pelo `PageNormalizer`; chaves futuras/desconhecidas são preservadas em vez de descartadas.
- Testes reproduzem o oráculo JS do capítulo vindo de nota e verificam round-trip/arquivo menor/proteção forward.
- Sem escrita de vault, Studio ou migração física; UC-15 continua aberta até CI e revisão do restante de paridade.

### 2026-10-06 — ChatGPT — UC-15 quarta fatia: layout livre

- Branch empilhada `feat/urbe-uc15-free-layout` criada sobre a terceira fatia enquanto #253 finaliza.
- Portados modelo/normalização e renderer puro do `pages/free.js`, incluindo estilos responsivos por peça e isolamento por seção.
- Segurança: medidas/cores/URLs/classes/CSS validados; embeds exigem HTTPS; conteúdo textual passa por Markdown/escape; HTML livre continua raw por contrato avançado.
- Testes cobrem árvore/ids, responsividade, injeção, peças novas, nota do vault, modos de folha de livro e clamp.
- Sem Studio/drag-drop/preview editável e sem escrita de dados reais; isso permanece fora do domínio desta fatia.

### 2026-10-06 — ChatGPT — UC-15 terceira fatia empilhada

- Enquanto o PR #252 executa o estado combinado, foi aberta a branch `feat/urbe-uc15-book-structure` sobre o head da segunda fatia.
- Novo `PageRenderPlan`: âncoras únicas, TOC, numeração de partes/capítulos e expansão de capítulos por pasta.
- Novo `PageBookRenderer`: TOC, capa, folha de rosto, sumário de livro, partes, capítulos e capítulos por pasta.
- PageNormalizer agora inclui defaults de layout/book do motor JS.
- Testes cobrem slug com acentos/duplicatas, hidden, numeração contínua, capítulo sem número, heading shift, capa segura e defaults.
- A branch é empilhada deliberadamente; só será retargetada para main depois que #252 entrar.

### 2026-10-06 — ChatGPT — UC-15 segunda fatia: blocos de página

- PR #250 foi integrado em `4a7dd74d`; a primeira fatia de UC-15 ficou verde no estado combinado.
- Nova branch `feat/urbe-uc15-page-blocks` parte da main atual.
- Port iniciado para blocos ricos/estrutura: features, cards, gallery, video, testimonials, stats, timeline, faq, cta, columns, pricing, contact, countdown, code, divider, html e blocos editoriais simples de livro.
- Segurança/paridade: links e mídia passam por SafeUrl; texto por escape/Markdown; `html` continua raw porque esse é o contrato avançado do cliente JS.
- Próximo: CI desta fatia; depois TOC/numeração e blocos estruturais de livro que dependem de contexto entre seções.

### 2026-10-06 — ChatGPT — UC-15 primeira fatia em verificação

- Draft PR #250: PageDocument lossless, PageNormalizer REQ-049, PageRenderer puro, CompositionFile/conversor e planner idempotente de migração.
- Prova parcial já verde no head: Core C# 170/170, Android e E2E; consistency PR/push verde. Windows/checks ainda finalizavam quando o handoff foi promovido para verifying.
- Escopo deliberado: nenhuma escrita/migração real no vault. A ligação compositions→pages será PR separado de classe user-data, com backup e autorização.
- UC-15 continua aberta após esta fatia; próximo passo é ampliar paridade de páginas sem misturar o corte de dados.

### 2026-10-06 — ChatGPT — UC-14 encerrada; UC-15 iniciada

- UC-14: PR #232 integrado automaticamente em `1eafe4db`; estado combinado verde e Issue #230 encerrada.
- UC-15: Issue #249 aberto; branch `feat/urbe-uc15-pages-compositions`.
- Primeira fatia: `PageDocument` lossless/proteção forward, `PageRenderer` puro e `CompositionFile` + conversão composição→página.
- Invariante: nenhum write/migration real no vault nesta fatia; composições antigas permanecem intactas e campos sem mapeamento exato são preservados.
- Próximo: rodar CI, corrigir divergências e então ampliar a paridade de páginas antes de qualquer ligação a hosts/dados reais.

### 2026-10-04 — ChatGPT — UC-5/UC-6 consolidadas e G-C0 aprovado

- DEC-0035-A aplicada: Blazor WebAssembly PWA + .NET MAUI Blazor Hybrid com RCL compartilhada.
- DEC-0036-C aplicada: reinstalação deliberada com backup/export/import obrigatório; sem ponte de upgrade in-place.
- ADR-0025/0026 passam a Aceito; UC-5/UC-6 concluídas; G-C0 aprovado.
- Próxima tarefa autorizada pelo roadmap: UC-8 — esqueleto C# e CI.

### 2026-10-04 — ChatGPT — hardening do aceite GC

- O mesmo head do PR #187 passou 297/297 em `urbe-checks`, mas o estado combinado falhou apenas em `ui-gc-apply`: o sidecar de GC já estava persistido e o `maintenance` de `.urbe/vault.json` ainda não.
- Corrigido somente o adapter da suíte: ele agora espera de forma limitada o registro realmente aparecer no vault persistido. O critério não foi afrouxado e runtime JS não foi alterado.

### 2026-10-04 — ChatGPT — UC-5/UC-6: propostas para decisão

- UC-5: ADR-0025 + DEC-0035. Recomendação A: Blazor WebAssembly PWA no Web e .NET MAUI Blazor Hybrid em Windows/Android, UI compartilhada em Razor Class Library; Avalonia 12, Uno e UI separada permanecem alternativas reais.
- Motivo técnico principal: o Urbe é editor-first e mobile-first; DOM/IME/teclado/acessibilidade pesam mais que a elegância de um renderer único. C# total = autoridade de produto em C#, não negar service worker/bootstrap/interoperabilidade inevitáveis do browser.
- UC-6: ADR-0026 + DEC-0036. Recomendação A: preservar package/assinatura/origem e usar ponte de atualização; separar troca tecnológica da futura migração first-party.
- Nenhum código C# nem corte foi autorizado. Issues #182/#183 ficam waiting até as escolhas no portal.

### 2026-10-04 — ChatGPT — UC-2 concluído

- FATO OBSERVADO: PR #174 foi integrado automaticamente em `2e088e7` após estado combinado verde; head `b732c957` também passou `urbe-checks` e consistency.
- Feito: UC-2 encerra M0 de paridade com 297 casos portáveis, os 14 E2E funcionais representados e 11 protocolos físicos Web/Windows/Android explicitamente `not-executed` até as fases de host/validação.
- Limite preservado: nenhum cliente C# foi iniciado e nenhuma pilha/estratégia de transição foi escolhida implicitamente.
- Próximo: UC-5 e UC-6 — preparar ADRs e decisões do proprietário para pilha de UI/hosts e transição de canais.

### 2026-10-04 — Codex — UC-2: fixtures no navegador

- FATO OBSERVADO: head 9c4d297 do PR #171 passou consistency/urbe: 260/260 casos e 5/5 E2E. Integrado automaticamente em a8e53ed0.
- Feito: 11 casos browser.vault derivados das fixtures congeladas, incluindo abertura/salvamento, backup original, sidecars e versões futuras/readonly no app real. Corpus aditivo 293; runtime intocado.
- Verificação: corpus/oráculo/projeção e testes Node focados verdes; 272/272 no CI (5f38b0b); expansão com 16 browser.storage exige nova rodada.
- Próximos passos: integrar PR #171, verificar esta fatia e seguir com migração IDB pelo boot, identidade/layout e ZIP. Handoff HO-20261004-urbe-browser-fixtures-parity.

### 2026-10-04 — Codex — UC-2: atualização e cliente unificado

- FATO OBSERVADO: PR #169 integrado; trabalho sobre 093ef4f inclui a versão-ponte #167.
- Feito: 19 casos portáveis Android/Windows e oito UI, corpus aditivo 260; cliente de referência unificado executa todas as famílias, DOM Visual/UI em Chromium real.
- Limites: UC-2 segue aberto; nenhuma escolha de UI/host nem código C#. Download local Chromium falhou, casos UI aguardam CI; atualização usa hosts/rede simulados, não valida instalação.
- Próximos passos: executar CI e corrigir divergências; continuar transcrição UI/E2E e lifecycle. Handoff `HO-20261004-urbe-update-ui-parity`.

### 2026-10-04 — Codex — UC-2: contrato nativo portável
- Estado: revisão; Issue #139; branch `chatgpt/urbe-uc2-native-parity`.
- Feito: 24 casos JSON por superfície com bridge/preload/main reais sobre hosts simulados; corpus 233. Capacidades/APIs, vault, bytes, caminhos inválidos, recusas de escrita/exportação, cancelamento, links, impressão, armazenamento/voltar e atualização Android offline.
- Decisões (fontes): ADR-0016/DEC-0024-B e DEC-0025-C; apenas tooling UC-2, runtime congelado e oráculo intactos.
- Pendências: UI/E2E portáveis; atualização/lifecycle/permissões reais; C# não iniciado. G-C0 não encerrado. Windows simulado usa filesystem local; não prova Windows instalado; Android simulado não prova aparelho.
- Próximos passos: continuar UC-2 com UI/E2E e atualização. Handoff `HO-20261004-urbe-native-parity`.

### 2026-10-03 — ChatGPT — UC-2: crash recovery portável (REQ-007/038/046)
- Estado: review no PR #165, baseado diretamente em `main` após integração do PR #161; gates finais em execução.
- Feito: quatro casos `vault.crash-recovery` injetam falha na criação do journal, no segundo arquivo, na remoção de nota e na remoção final do journal. O corpus passa de 205 para 209 casos.
- Invariante: nenhum arquivo de runtime congelado foi alterado; somente tooling, testes e documentação do corpus UC-2.
- Restante após esta fatia: capacidades nativas portáveis e UI/E2E por superfície. Rollback atômico de restore pode ser hardening futuro, não bloqueio do REQ-038/046.



### 2026-10-03 — ChatGPT — UC-2: IndexedDB legado e gestão de vaults (REQ-007/028/046)
- Estado: integrado na `main` pelo PR #161; Issue #139 continua aberta para as fatias restantes de UC-2.
- Feito: quatro cenários portáveis IDB/browser adicionados ao `storage.scenario` e um caso separado para a migração v1 `kv["cidade"]`; corpus candidato 205. O `idb.js` real é executado sobre IndexedDB determinístico do tooling, cobrindo chaves legadas `knowledge-city`, gestão/isolamento de vaults, pastas e binários.
- Limites: runtime JS congelado não foi alterado; o harness determinístico não substitui `tests/e2e/adapters.e2e.mjs` em Chromium nem valida aparelho/permissão do SO. UC-2 segue aberto.
- Verificação: gates próprios e estado combinado do integrador passaram antes do merge.

### 2026-10-03 — codex — UC-2: identidade/GC (REQ-042)
- Estado: revisão parcial, Issue #139; branch codex/urbe-domain-parity.
- Feito: 26 cenários portáveis; 200 casos, 64/64 testes e 22 checks verdes. UC-7 reconciliado após PR #151.
- Limites: nenhuma feature futura, formato ou runtime alterados; UC-2 segue aberto. Visão de produto recebida será tratada em branch própria.

### 2026-10-03 — codex — UC-2: operações de storage (REQ-007/028/055)
- Estado: revisão parcial, Issue #139.
- Feito: nove casos JSON de storage, adapter de referência com FSA real sobre ponte nativa em arquivos temporários; corpus total 174.
- Verificado: Unicode, bytes, hidden dirs/sidecars, overwrite/remove, pasta não vazia, traversal e recusa injetada; mutações negativas.
- Limites: não prova permissão do SO, browser IDB ou aparelho; gestão de vaults/capacidades nativas/UI ainda pendentes. Nenhum runtime/formato alterado.

### 2026-10-03 — codex — UC-3/UC-4 (REQ-007/023/035/038/042)
- Estado: revisão, Issues #146/#147.
- Feito: contrato gerado das autoridades de dados, manifesto integral das 12 fixtures e gate contra divergência; inventário de dependências com fontes primárias e riscos por plataforma.
- Decisões: nenhuma pilha escolhida; vault/formato e runtime preservados (ADR-0016, DEC-0025-C).
- Pendências: UC-2 parcial, UC-7 crítico, UC-5/6 requerem decisões; C# ainda sem implementação.
- Próximos passos: UC-2/7 e, depois dos fundamentos, UC-5/6.

### 2026-10-03 — codex — UC-1 / UC-2 (REQ-002/004/007/016/027/037)

## 2026-10-03 — codex — UC-2: vault e restauração

12 fixtures convertidas para casos JSON portáveis e três casos de restauração válida/corrompida/ausente. Adapter calcula saídas do domínio JS em memória; verifica hashes, IDs, proteção forward, recuperação e backup. Baseline congelado preservado. UC-2 (#139) continua parcial; nenhum runtime ou formato alterado.

- Estado: revisão, Issues Ecosystem #138/#139; base 2e4cfa902c86d2eb2441bacfa6b29267f7e46c9e.
- Feito: matriz dos 101 REQ IMPLEMENTAR com fontes/aceite e inventário de testes/tutorial; oráculo SHA-256 incluindo .urbe; 40 casos Markdown e 110 Visual como JSON portável; protocolo de cliente e runner que recusam resultados incompletos/divergentes; adapter JS calcula Markdown real.
- Decisões (fonte): DEC-0024-B / ADR-0016 e DEC-0025-C; runtime JS congelado. Sem escolha de pilha, mudança de vault/canal, ou código C# de produto.
- Pendências: UC-2 não encerrado: converter cenários de vault, adapters e UI; C# não existe e nenhuma paridade em aparelho foi alegada. G-C0 segue não iniciado.
- Próximos passos: restante de UC-2; UC-3/4/7; propostas UC-5/6 depois da evidência. Handoff: docs/governance/handoffs/HO-20261003-urbe-csharp-parity.json (raiz Ecosystem).

# AGENTSCHAT.md — Log de coordenação entre agentes e proprietário

> Append-only. Mais recente no topo. Leia antes de trabalhar; escreva ao terminar (formato em `AGENTS.md` §7).
> Não guarde segredos aqui. Decisões normativas devem também constar em REQUIREMENTS/ADR.

---

### 2026-10-03 — Claude — DEC-0025-C aplicada: Urbe JavaScript congelado, salvo bug crítico
- **Estado:** o proprietário decidiu a **alternativa C** pelo portal (Ecosystem DEC-0025, Issue #68, registro `docs/governance/responses/DEC-0025.md`): congelamento total do JavaScript, salvo bug crítico. Esta entrada **não implementa nada**: aplica as consequências nos documentos.
- **Feito:** nota de congelamento com justificativa no topo de `docs/v2/ROADMAP.md` (itens `[ ]`/`[~]` adiados, **nenhum item, REQ ou gate removido**); `docs/csharp/README.md`, Urbe ADR-0010 e Ecosystem ADR-0016 deixam de dizer «pendente».
- **Regra daqui em diante:** não iniciar nem continuar item do 2.0 em JavaScript (nem preparação de fixtures/goldens); bug crítico do produto distribuído segue sendo corrigido, com regressão. A preparação do oráculo da paridade passa a ser do programa C# (UC-1..UC-4).
- **Não decidido:** quando o congelamento termina (o corte, UC-31, é decisão do proprietário).

### 2026-10-02 — Claude — DEC-0024-B aplicada: programa de migração do Urbe para C# (UC-n), planejamento
- **Estado:** o proprietário decidiu a **alternativa B** (reescrita completa em C#, troca do produto só em paridade total) pelo portal (Ecosystem DEC-0024, Issue #45, registro `docs/governance/responses/DEC-0024.md`). Esta entrada **não implementa nada**: aplica as consequências nos documentos.
- **Feito:** Ecosystem ADR-0016 → Aceito; Urbe **ADR-0010** (referência qualificada ao Ecosystem ADR-0016); programa em `docs/csharp/` (README + ROADMAP `UC-1..UC-32`, fases M0–M5, gates G-C0–G-C5); documentos do Ecosystem que diziam «Urbe permanece em JavaScript / reescrita proibida» corrigidos.
- **Não decidido (cada um tem item e decisão própria):** pilha de UI/hosts (UC-5), transição das instalações e canais (UC-6), modelo de plugins em C# (UC-20), o corte (UC-31). A pilha do exemplo (Blazor etc.) NÃO foi decidida.
- **Nova decisão para o proprietário:** Ecosystem **DEC-0025** (não bloqueante): o que fazer com os itens estruturais abertos do programa 2.0 em JavaScript. Padrão conservador enquanto pendente: nenhum item estrutural novo do 2.0; correções e preparação de paridade seguem.
- **Regras:** dados do usuário não mudam de formato; paridade provada por fixtures/goldens/E2E independentes de linguagem; zona crítica da política precisa cobrir o código novo (UC-7) antes do primeiro PR de código C#.
- **Próximos passos:** M0 (UC-1 matriz de paridade, UC-2 suíte de aceite, UC-3 contrato do vault, UC-4 dependências, UC-7 política), em PRs próprios.

### 2026-10-02 — Claude — correção: código inline com wikilink (achado de RM-F2-09/10), U-R1 no monorepo Ecosystem
- **Estado:** corrigido em PR próprio (NN-013: refatoração e correção separadas). RM-F2-10 segue `[~]`.
- **Bug:** `` `[[x]]` `` renderizava com U+0002 dentro do `<code>` (o `inlineMarkdown` v22 trocava wikilinks antes do código).
- **Correção** (`src/editor/markdown.js`): o código sai primeiro e é opaco; marcadores aninhados voltam em mais de uma passada; `alt` de imagem recebe o código como texto. Efeito colateral deliberado: código dentro do rótulo de um link/ênfase agora renderiza como `<code>` (antes ficava com crases literais).
- **Prova:** o golden de `markdown.js` mudou em exatamente 2 de 33 casos, os dois com o marcador vazado; 7 casos de regressão novos (40 no total) e asserção de que nenhuma saída contém U+0001..U+0003; 4 entradas do golden do serializador atualizadas. `npm run check` e 14 E2E verdes.
- **CHANGELOG:** a entrada vai na próxima release (o arquivo só lista versões publicadas).

### 2026-10-02 — Claude — RM-F2-10 `[~]` (1ª fatia: serializador Visual → Markdown), U-R1 no monorepo Ecosystem
- **Estado:** RM-F2-09 `[x]` (PR #54). RM-F2-10 em andamento: a parte pura saiu; o editor Visual com estado ainda não.
- **Feito:** `src/editor/visual.js` (`UrbeVisual`, serviço `editor.visual`) com `markdownFromVisual` e as duas camadas que o embrulhavam (U+200B/`#` vazio e `UrbeMathEditor`); `app.js` ficou com um adaptador de uma linha que injeta o frontmatter (−113 linhas; teto rebaixado). Golden de 110 casos gerado em Chromium com o código antigo; `tests/e2e/app-runtime-visual.e2e.mjs` (nome casa com o filtro `app-runtime` do CI) + caminho real do editor; mutação da normalização derrubou o teste. Contrato `docs/v2/contracts/editor-visual.md`. `tests/ai-agent.mjs` e `tests/search-editor.mjs` passaram a ler o serializador em `visual.js`.
- **Achado (bug pré-existente, não corrigido aqui por NN-013):** código inline com wikilink (`` `[[x]]` ``) renderiza com caracteres de controle `\x02`: o `inlineMarkdown` com sanitização (v22) troca wikilinks antes do código; a versão base tratava o código primeiro. Está travado no golden de `markdown.js` (2 casos) e entra como correção em item/PR próprio, atualizando o golden de propósito.
- **Falta de RM-F2-10:** `setEditorViewMode`, sincronização Visual↔fonte, `contenteditable`/seleção, estado vazio e «sem ressuscitar nota apagada»: dependem de closures do app.js e pedem validação em aparelho (NN-017).
- **Próximos passos:** corrigir o bug do código inline; depois RM-F2-05..08 (renderer/laço de desenho; fecham o `#mini` e RM-F2-04).

### 2026-10-02 — Claude — RM-F2-09 `[x]` (renderMarkdown fora do app.js), U-R1 no monorepo Ecosystem
- **Estado:** RM-F2-09 concluído; RM-F2-04 segue `[~]` (falta só o minimapa legado).
- **Feito:** `src/editor/markdown.js` (global `UrbeMarkdown` + serviço `editor.markdown`): render de blocos, inline, frontmatter, tabelas, callouts, `safeUrl`, cabeçalho vazio e a camada de matemática — copiados do `app.js`, que agora só desestrutura o global no topo. `app.js` −201 linhas (5199 → 4998) (teto de dívida rebaixado). Contrato em `docs/v2/contracts/editor-markdown.md`.
- **Prova de equivalência:** `tests/fixtures/markdown-golden.json` foi gerado do código antigo (33 casos) e `tests/markdown.mjs` exige saídas idênticas; o preview no app real está em `tests/e2e/app-runtime.e2e.mjs` (mutação da sanitização derruba o teste). `npm run check` 59/59 e os 13 E2E passam.
- **Achado:** a exceção `pages/engine → app.js` era falso positivo (a palavra «URBE» num texto casava com o global `URBE`, que saiu em RM-F2-04); removida de `BOUNDARY-EXCEPTIONS.md`. O nome do serviço ficou `editor.markdown` (o roadmap dizia `editor.surface`).
- **Pendências:** `markdownFromVisual` e o editor Visual ainda em `app.js` (RM-F2-10/11); nada para o proprietário.
- **Próximos passos:** RM-F2-10, depois RM-F2-05/06/07/08 (renderer/laço de desenho; removem o `#mini`).

### 2026-10-02 — Claude — RM-F2-04 `[~]` (L11 feito; L10 feito exceto o minimapa), U-R1 no monorepo Ecosystem
- **Estado:** RM-F2-02 `[x]` (E2E no CI, PR #47). RM-F2-04 em andamento: `window.URBE` removido e resíduos de UI (L10) saíram, menos o minimapa legado `#mini` (depende do laço de desenho: RM-F2-05/08).
- **Feito:** `app.js` −61 linhas (teto de dívida rebaixado); `index.html`/CSS sem rodapé do Explorador, botão de importar arquivos e diálogo antigo de Vault/cidade; serviço `diagnostics.world` no lugar do gancho global (3 E2E migrados); `tests/legacy-ui-residue.mjs` (grep zero); LEGACY-MAP L10/L11 atualizado.
- **Achado:** o LEGACY-MAP dizia que `window.URBE` não tinha consumidor; tinha (`URBE.mundo` em 3 E2E). Corrigido no mapa.
- **Mantido de propósito:** `vaultFolderIn`/`vaultFilesIn` (fallback de seleção de pasta/arquivos) e `fromGlyph` — são funcionais.
- **Verificação:** `npm run check` (58 testes) e E2E `app-runtime zip smoke stable-ids layout identity lifecycle` (7/7) no Chromium real.
- **Próximos passos:** RM-F2-09 (renderMarkdown para o editor, remove a exceção `pages/engine → app.js`); depois RM-F2-05/06/07/08.

### 2026-10-02 — Claude — RM-F2-01 `[x]` e RM-F2-02 `[?]` (U-R1, monorepo Ecosystem)
- **Estado:** RM-F2-01 concluído (relatório + script + teste). RM-F2-02 implementado e verde localmente; `[?]` porque os E2E não rodam no CI (nenhum workflow instala o Chromium).
- **Feito:** `tools/hotspots.mjs`, `docs/v2/discovery/HOTSPOTS.md` (métricas geradas + decisão por arquivo: nenhum dos 7 é extraído agora), `tests/hotspots.mjs`; `tests/e2e/app-runtime.mjs` (harness) e `app-runtime.e2e.mjs` (boot, fixture, criar nota, mundo, recarregar).
- **Decisões (com fonte):** harness em `tests/e2e/` e não em `tests/` (RM-F2-02 dizia `tests/app-runtime.mjs`): `npm test` roda todo `tests/*.mjs` sem navegador. Registrado no item.
- **Achado (proposta, não decisão):** `pages/studio` tem o maior acoplamento de UI fora do `app.js`; resolver `studio → ai/ui` exige item novo + ADR (HOTSPOTS §3).
- **Verificação:** `node tests/hotspots.mjs`; `node tools/run-e2e.mjs app-runtime zip smoke`; `npm run check`.
- **Próximos passos:** RM-F2-04, RM-F2-09 (usa o harness). Zona crítica da política de integração: `src/persistence/**` — não tocar sem aprovação.

### 2026-10-02 — Claude — governança: integração rotineira automática (ADD-0012 do Ecosystem)
- **Estado:** a regra de merge do Urbe em `AGENTS.md` §3 mudou (Ecosystem PR #40, integrado pelo proprietário): trabalho **rotineiro** do Urbe entra na `main` sozinho pelo integrador do Ecosystem, com `npm run check` e o CI verdes no estado combinado; **crítico** (dados, fronteira de confiança do desktop/plugins/credenciais, assinatura/identidade/canal, licença, ADR consolidado) espera a autorização do proprietário.
- **Feito:** datas corrigidas (o encerramento do RM-F2-03 foi em 2026-10-01, não 10-02) neste log e em `docs/v2/ROADMAP.md`. Esta mudança é o canário rotineiro: não toca zona crítica.
- **Decisões (com fonte):** ADD-0012 e ADR-0015 do Ecosystem; zonas críticas em `docs/governance/integration-policy.json` do Ecosystem.
- **Pendências / bloqueios:** nenhuma.
- **Próximos passos:** U-R1 segue com RM-F2-01 e RM-F2-02, em tarefas próprias.

### 2026-10-01 — Claude — RM-F2-03 (JSZip em `vendor/jszip`), primeiro item de U-R1 no monorepo Ecosystem
- **Estado:** RM-F2-03 `[?]`: implementado e verificado automaticamente; falta validar import/export ZIP no navegador (`tests/e2e/zip.e2e.mjs`, não executado: sem Playwright instalado nesta sessão). O Urbe agora é desenvolvido em `apps/urbe/` do Ecosystem (ADR-0009 do Ecosystem; refatoração interna R).
- **Feito:** `src/legacy/bootstrap.js` movido (git mv, conteúdo idêntico) para `vendor/jszip/jszip.min.js`; `vendor/jszip/LICENSE`; `src/modules.json`, `index.html`, `sw.js`, `tools/gen-modules.mjs` e `THIRD-PARTY-NOTICES.md` atualizados; `src/legacy/` removido. Nenhum formato de dados tocado.
- **Verificação:** `node tools/check-modules.mjs` OK (73 módulos); `npm run check` verde (56/56 arquivos de teste).
- **Pendências:** texto de `vendor/jszip/LICENSE` escrito a partir do cabeçalho do arquivo (MIT, opção já adotada em THIRD-PARTY-NOTICES) — conferir com a licença upstream; validação humana do ZIP; origem `AbnerCruz/Urbe` precisa da sincronização (`sync-from-ecosystem.yml`) depois do merge.
- **Próximos passos:** RM-F2-01 (HOTSPOTS) e RM-F2-02 (harness do monólito); F1 ainda tem RM-F1-20…26 abertos.
- **Integração (2026-10-01, agente integrador):** branch `ccr-5adec8cc-oy464b`, PR #35 do Ecosystem (reconciliado com a `main` depois da Fase 3; sem conflito). `tests/e2e/zip.e2e.mjs` rodou em Chromium real e passou (1/1). RM-F2-03 segue `[?]` até o merge, que **só acontece com pedido explícito do proprietário**; depois, disparar `sync-from-ecosystem.yml` em `AbnerCruz/Urbe`.
- **Encerramento (2026-10-01):** o proprietário pediu o merge ("Pode fazer o merge e aprovar tudo"; ADD-0011 do Ecosystem). RM-F2-03 `[x]` no mesmo PR que o integra (estado combinado reconciliado com a `main` e verde). Próximos itens de U-R1: RM-F2-01 e RM-F2-02, em tarefas próprias.

### 2026-09-30 — Claude — RM-F1-10…19 (persistência, identidade, mundo, export, multi-cidade)
- **Estado:** F0 quase completa e F1 até RM-F1-19 `[x]`. PR #36 (F0 + RM-F1-01…13) mergeado na `main` (`3351aaa`) com CI verde, como autorizado ("se estiver validado e funcionando é pra entrar na main"). RM-F1-14…19 estão na branch `claude/new-session-eh5rwa`, com PR próprio.
- **Feito:**
  - RM-F1-10/11/12: adaptadores `idb`/`fsa`/`router` extraídos de `app.js`, com suíte de contrato em Node e em IDB/OPFS reais.
  - RM-F1-13: escritor único do `mapa.json` (`metadataProvider`) e validação de `mapa.v`.
  - RM-F1-14: IDs `reg_`/`ast_` e `parentId`.
  - RM-F1-15: `.urbe/identity.json` e reconciliação de rename externo, com o app fechado e aberto.
  - RM-F1-16: GC de órfãos com retenção e registro.
  - RM-F1-17: reorganização da cidade sempre com backup e desfazer.
  - RM-F1-18: `urbe-export.json` com hashes, estado local sem segredos e import de `.zip` pelo Explorer (bug da 1.8.2).
  - RM-F1-19: multi-cidade idempotente, com fusão do mapa pelo `WorkspacePersistence`.
  - Código morto apagado do `app.js`: ~450 linhas; teto de dívida rebaixado a cada item.
- **Decisões (com fonte):**
  - Casa só guarda a posição se a nota continua no mesmo bairro (DATA-CATALOG §4, RM-F1-15).
  - Lixeira só expira com `trashDays` explícito (RM-F1-16).
  - Export de formato futuro é recusado (DATA-CATALOG §9).
  - `nomeSeguro` virou `UrbeArtifacts.safeName`, fonte única de nome de arquivo.
- **Pendências / bloqueios:**
  - RM-F0-12: baseline L de performance não gerado.
  - RM-F0-15: SHAs das actions exigem fonte confiável e aprovação.
  - RM-F0-16: exclusão das 31 branches remotas foi negada pelo classificador e precisa de autorização explícita do proprietário.
  - O hardening de Electron/Android foi testado só com simulação.
- **Próximos passos:** RM-F1-20…26, depois F2 (monólito/legacy), F3, F4, F5, F6 e F7.

### 2026-09-30 — Claude (Sonnet 5.5) — aprovação do proprietário e integração na `main`
- **Estado:** SPEC e ROADMAP **aprovados**; OD-05, OD-08, OD-10, OD-11 **aprovadas**; licença **definida**. Implementação da 2.0 continua NÃO iniciada.
- **Decisões do proprietário (mensagem de 2026-09-30):** "Todos os direitos reservados, tudo meu" (OD-03 → ADR-0003); "Se estiver validado e funcionando é pra entrar na main sim" (autoriza a integração desta branch na `main` após validação: CI verde e sem alteração de runtime); "De resto aprova tudo" (SPEC, ROADMAP, ADR-0005/0006/0007, OD-05/08/10/11).
- **Feito:** ADRs 0003–0007 → Accepted; `RM-F4-12` desbloqueado (`[ ]`); documentos atualizados; TRACEABILITY regenerada.
- **Pendências:** budgets absolutos de performance (RM-F5-02, após o baseline); OD-06 e OD-09 seguem adiadas com gate.
- **Próximos passos:** iniciar F0 (RM-F0-03…18) em sessão própria; RM-F0-16 trata as branches `claude/*`.

### 2026-09-30 — Claude (Sonnet 5.5) — descoberta da issue #33 e planejamento integral da 2.0
- **Estado:** descoberta concluída; SPEC, ROADMAP, TRACEABILITY e ADRs 0001–0007 produzidos. **Implementação da 2.0 NÃO iniciada.** Runtime 1.8.2-beta intocado.
- **Feito:** merge fast-forward de `origin/chore/urbe-2-foundation` (PR #34 não mergeado; fundação preservada) na branch `claude/new-session-eh5rwa`; `docs/v2/discovery/*` (9 documentos), `REQUIREMENTS.md` (REQ-025..088 acrescentados, nenhum removido), `adr/0001..0007`, `SPEC.md`, `ROADMAP.md` (118 itens, F0–F6), `TRACEABILITY.md` (gerado), `AUDIT-COVERAGE.md`, `tools/check-traceability.mjs`, `tools/gen-traceability.mjs`, `tests/traceability.mjs`, `AGENTS.md`, este arquivo.
- **Decisões do proprietário (respostas em 2026-09-30):**
  1. Build: por enquanto **sem build**; launcher que centralize os aplicativos do proprietário é ideia futura, fora da 2.0-beta (ADR-0001; REQ-033/034 ADIADOS).
  2. Plugins: **full-trust aprovado com UX honesta** (ADR-0002; REQ-052 ADIADO).
  3. Licença: **fonte-disponível/proprietária** (ADR-0003); texto final da `LICENSE` pendente.
  4. Compatibilidade: **ler tudo da 1.x; escrever 2.x com proteção forward** (ADR-0004).
  5. Pedido: incluir `AGENTS.md` e `AGENTSCHAT.md` (REQ-088).
- **Propostas aguardando confirmação (adotadas na SPEC como padrão, sujeitas a veto):** OD-05 release por tag (ADR-0005); OD-10 identidade em sidecar (ADR-0006); OD-11 arquivos `*.v2.json` paralelos (ADR-0004); OD-08 mitigação de chaves de IA + CSP (ADR-0007).
- **Pendências / bloqueios:** texto da `LICENSE` (OD-03) bloqueia o gate G4 (RM-F4-12 `[!]`); budgets absolutos de performance dependem do baseline (RM-F5-02); atualização do checklist da issue #33 e criação de PR dependem do proprietário/ferramentas GitHub.
- **Próximos passos:** proprietário revisa e aprova SPEC/ROADMAP (responder OD-05/10/11/03); depois iniciar F0 (RM-F0-03…18). Nada da 2.0 é implementado antes disso.
- **Branches:** 31 branches `claude/*` remotas aguardam verificação (RM-F0-16); esta sessão não removeu nenhuma.

### 2026-10-04 — Codex — P4-9 / RM-F4-10 / REQ-006/066/081
- Estado: verificando correção do PR #167 no estado combinado com main@46c295a.
- Feito: corrigido parêntese excedente no teste de feed, fixture Android alinhada ao feed direto e revisão explícita de dois hashes do baseline autorizada pela mudança de distribuição.
- Decisões (com fonte): ADD-0015/DEC-0031/ADR-0019; nenhuma mudança de dados ou de assinatura.
- Pendências: CI combinado, autorização crítica e chave histórica/release/DEVICE para corte final.
- Próximos passos: integrador reavalia o PR; P4-9 continua aberto.

### 2026-10-04 — Codex — UC-8 / REQ-001/008/016/064
- Estado: base C# verificada localmente na branch feat/urbe-uc8-csharp-foundation; Issue #192; envio remoto bloqueado pela revisão automática.
- Feito: Core puro, UI Razor compartilhada, Web WASM/PWA e host MAUI Hybrid Windows/Android; soluções portátil/nativa separadas; testes de boundary/composição e smoke Chromium publicado; urbe-checks estendido.
- Decisões (com fonte): DEC-0035-A/ADR-0025 e DEC-0036-C/ADR-0026, G-C0 integrado no PR #191. Identidade privada de build nativo dev; versão pública continua package.json.
- Verificação: 8 testes C#, Core/RCL/Web compilados, publish Release e smoke Chromium online/offline em raiz/subpasta, npm check 66/66 e consistency 22/22. MSBuild WASM/ILLink executado in-process por targets temporários externos ao checkout devido ao IPC restrito local.
- Pendências / bloqueios: revisão automática rejeitou git push por autorização explícita de publicação ausente; sem alternativa indireta. CI/builds nativos e integração crítica por csproj/Platforms/workflow continuam pendentes; nenhuma validação física alegada.
- Próximos passos: verificar CI e corrigir; após integração UC-8, UC-9 (leitura do vault contra fixtures com proteção forward).

### 2026-10-04 — Codex — UC-8 envio autorizado
- Estado: proprietário autorizou explicitamente envio da branch e abertura de PR; CI em verificação.
- Bloqueio anterior de autorização resolvido. Git HTTPS local não tem credencial; envio via conector GitHub.


### 2026-10-04 — Codex — UC-9 / REQ-007/035/036/037/038/042
- Estado: implementação do leitor C# em `feat/urbe-uc9-vault-reader`; Issue #199 em `state:working`; CI e integração crítica ainda pendentes.
- Feito: `Urbe.Core.VaultReader` puro recebe bytes em memória e projeta documentos/mapa/sidecars sem filesystem nem escrita; cobre formatos 1.x/2.x, precedência v2→v1, journal recuperável, IDs/posições conhecidos e proteção forward por artefato/vault.
- Decisões (com fonte): DATA-CATALOG §9, ADR-0004 e VAULT-CONTRACT; journal válido muda somente o estado efetivo lido e os bytes físicos permanecem intactos; sidecar v2 presente e futuro/corrupto nunca cai para v1; vault futuro torna metadados de sistema protegidos.
- Verificação: suíte C# adicionada para as 12 fixtures canônicas e negativos de precedência, formato futuro, corrupção, traversal, duplicata e journal. Execução real fica a cargo do CI porque esta sessão não dispõe de SDK .NET local.
- Pendências / bloqueios: CI do PR; autorização canônica do proprietário para integração, pois UC-9 é crítico por dados do usuário. Nenhuma gravação, migração, backup ou integração aos hosts foi implementada.
- Próximos passos: abrir PR, corrigir qualquer falha do estado combinado e, só após UC-9 integrado, avançar para UC-10.


### 2026-10-04 — Codex — UC-9 verificado
- Estado: PR #203 no head 5fa0d115; implementação pronta para revisão crítica, sem integração.
- Verificação: urbe-checks 37213623887 verde em C# portátil (33/33), Android, Windows, E2E e npm checks; consistency 37213623896 verde.
- Revisão adversarial adicional corrigiu duas divergências antes da integração: IDs não persistidos voltam a ser UUIDs novos por abertura, como no 1.x; decodificação textual segue UTF-8 tolerante do navegador, preservando os bytes físicos originais.
- Bloqueio único: autorização canônica do proprietário para integrar mudança classificada como user-data. UC-10 não começa antes da integração de UC-9.


### 2026-10-04 — ChatGPT — UC-10 / Issue #208 / PR #209
- Estado: escrita segura do vault em implementação na branch `feat/urbe-uc10-vault-writer`; UC-9 integrada pelo PR #203 e Issue #199 encerrada.
- Feito: motor puro de mutações, backup restaurável 1→2, migração idempotente, sidecars v2 preservando v1, journal multi-arquivo, vault.json, identidade documental e restauração; GC usa o mesmo escritor, com dry-run/retenção/maintenance.
- Paridade: fingerprint FNV/normalização/`seen` seguem `identity.js`; ordem e proteção forward seguem `workspace.js`; backup segue `backup.js`; GC segue `gc.js`.
- Testes: nova suíte cobre fixtures `v1-mapa-v4`, `futuro-v2`, `vault-futuro` e `v1-orfaos`, além de journal e identidade. CI do PR é a autoridade de compilação/regressão.
- Limites: nenhum adapter Web/MAUI/filesystem conectado; UC-11 (ZIP) não foi antecipada. Integração continua crítica por dados do usuário.


### 2026-10-04 — ChatGPT — UC-10 verificada
- Estado: PR #209 no head `83ed0c77` tecnicamente concluído e movido para review crítico.
- Evidência: `urbe-checks` 37217933358 verde em checks, C# portátil, MAUI Windows, MAUI Android e E2E; 43/43 `Urbe.Core.Tests` passaram. `consistency` 37217933366 verde.
- Gate: G-C1 agora está aguardando exclusivamente a integração crítica autorizada do PR #209; nenhuma nova implementação do Core é necessária antes disso.
- Próximo passo após integração: fechar Issue #208, marcar UC-10 concluída, aprovar G-C1 e iniciar UC-11 (ZIP + manifesto).


### 2026-10-04 — ChatGPT — UC-10 integrada / G-C1 aprovado
- PR #209 autorizado pelo proprietário via label canônica `integrar` e integrado pelo integrador em `590c9328`.
- O estado combinado testado passou 43/43 testes C#, Web/portable, Android, Windows, E2E, checks e consistency.
- UC-10 encerrada; G-C1 aprovado. O Core agora possui leitura e escrita do vault com migração/backup/identidade/GC sob o contrato histórico.
- Próxima tarefa: UC-11 — export/import ZIP + manifesto, ainda sem acoplar filesystem/hosts.


### 2026-10-04 — ChatGPT — UC-11 / Issue #212 / PR #213
- Estado: export/import ZIP + manifesto em implementação na branch `feat/urbe-uc11-export-import`.
- Feito: `VaultExportManifest` v1 com size/SHA-256, parse current/corrupt/future, verificação de adulterados/faltantes/extras, allowlist de estado portátil e remapeamento de aprovações de plugins; `VaultArchive` usa apenas BCL/System.IO.Compression.
- Segurança: journals v1/v2 ficam fora do export; `.urbe/**` e binários preservam bytes; import valida caminhos brutos antes de remover prefixo raiz, rejeitando traversal/absolutos/duplicatas case-insensitive; manifesto futuro é recusado e manifesto inválido não ganha autoridade.
- Verificação parcial: csharp-portable do run 37220350132 compilou Release e passou 61/61 testes, 0 falhas/0 skips; Android também verde. Regressão completa do head ainda em andamento e será repetida após documentação/handoff.
- Limites: sem filesystem, UI, download, picker ou localStorage no Core; hosts continuam fora do escopo da UC-11.


### 2026-10-04 — ChatGPT — UC-11 verificada
- Estado: PR #213 no head `52a57747` tecnicamente concluído e movido para review crítico.
- Evidência: `urbe-checks` 37220827485 verde em checks, C# portátil, MAUI Windows, MAUI Android e E2E; 63/63 `Urbe.Core.Tests` passaram, 0 falhas/0 skips. `consistency` 37220827469 verde.
- Revisão adversarial final: o import preserva `.urbe/` quando ela é a própria raiz do vault e classifica o envelope de manifesto futuro como future antes de interpretar o schema interno, evitando downgrade perigoso para corrupt.
- Bloqueio único: autorização canônica do proprietário para integrar mudança crítica de dados do usuário.
- Próximo passo após integração: encerrar Issue #212 e iniciar UC-12 — documentos, artefatos e índice de conhecimento.


### 2026-10-04 — ChatGPT — UC-11 integrada / M1 concluído
- PR #213 integrado manualmente pela conta proprietária `AbnerCruz` em `4b572d4f`.
- Após o merge, o head final foi reconfirmado: `urbe-checks` 37222912657 e `consistency` 37222912663 verdes; C# portátil, Android, Windows, E2E e checks passaram, com 63/63 testes C#.
- UC-11 encerrada. M1 — Núcleo de dados — está completo: leitura, escrita, migração/backup/identidade/GC e export/import ZIP estão no Core C#.
- Próxima tarefa: UC-12 — documentos, artefatos e índice de conhecimento.


### 2026-10-04 — ChatGPT — UC-12 / Issue #216
- Estado: documentos, artefatos e índice de conhecimento em implementação na branch `feat/urbe-uc12-documents-knowledge`.
- Feito: `ArtifactModel` porta a classificação única de artefatos, linkabilidade, extensões, safeName e roteamento puro; `DocumentStore` porta documentos imutáveis, parsing de frontmatter/wikilinks/tags, lifecycle e revisão; `KnowledgeIndex` porta aliases, links/backlinks, tags, tokens, busca e stats com reindex automático.
- Integração: o store aceita projeção de `VaultSnapshot.Documents` preservando IDs; não lê/escreve filesystem nem reinterpreta identidade persistida.
- Testes: suíte de paridade adicionada a partir de `tests/artifacts.mjs` e `tests/documents.mjs`, com negativos de código, aliases, artefatos não-linkáveis, path case-insensitive, lifecycle/revision e remoção/reindex.
- Limites: mundo UC-13, Markdown Visual UC-14, páginas/composições UC-15, quick-open/UI UC-18 e hosts permanecem fora do escopo.


### 2026-10-04 — ChatGPT — UC-12 verificada
- Estado: PR #218 no head `31bace9b` tecnicamente concluído e movido para review; mudança classificada como rotineira.
- Evidência: `urbe-checks` 37225693553 verde em checks, C# portátil, MAUI Windows, MAUI Android e E2E; 114/114 `Urbe.Core.Tests` passaram, 0 falhas/0 skips. `consistency` 37225693548 verde.
- Revisão de paridade: testes corrigidos para refletir a ordem real de tags e a sanitização real do JS; `DocumentStore` agora replica também o fallback falsy para id/title/created/modified vazios.
- Próximo passo: integração automática rotineira; depois UC-13 — projeção do mundo e bairros com IDs estáveis.


### 2026-10-04 — ChatGPT — UC-12 integrada
- PR #218 integrado automaticamente em `79c5be01`; o estado combinado testado tornou-se a `main`.
- UC-12 encerrada com 114/114 testes C#, checks, Android, Windows, E2E e consistency verdes.
- M2 continua com UC-13: projeção do mundo e bairros, com IDs estáveis; terreno, vida/simulação e renderização permanecem fora deste passo.


### 2026-10-04 — ChatGPT — UC-13 / Issue #221 / PR #223
- Estado: projeção do mundo/bairros em implementação na branch `feat/urbe-uc13-world-projection`.
- Feito: `WorldStableIds` porta FNV-1a/base36, preservação de IDs, colisões, idempotência, `regionFor` e fresh IDs; `WorldMapMetadata` projeta mapa v1/v2/v4 e preserva chaves desconhecidas; `WorldProjection` porta spatial metadata, regiões por caminho, rename por ID estável, revision/events e metadata de volta ao mapa.
- Segurança de dados: mapas futuros/corruptos não recebem IDs gerados e não podem produzir metadata para persistência; helper de VaultSnapshot recusa snapshot recuperado por journal enquanto o JSON efetivo completo de regiões não estiver disponível.
- Paridade revisada: lookup espacial/região continua case-sensitive como `Map` do JS; ID documental permanece case-insensitive via DocumentStore; `files:[]` mantém a semântica truthy do JavaScript e não cai para `anexos`.
- Limites: terreno/biomas, layout/reorganização, AquariumWorld, ruas, NPCs/vida e UI estão fora da UC-13.


### 2026-10-04 — ChatGPT — UC-13 verificada
- Head de código verificado: `281f5234`.
- `urbe-checks` #37230492613: Core C# 134/134 testes, Web smoke, Android, Windows, checks e E2E todos verdes.
- `consistency` #37230492604: verde.
- Issue #221 movida para `state:review`; PR #223 segue rotina automática de integração.


### 2026-10-04 — ChatGPT — UC-13 integrada
- PR #223 integrado automaticamente em `ccab4b91`; a `main` passou a conter a projeção pura do mundo/bairros e IDs estáveis.
- UC-13 encerrada com 134/134 testes C#, Web/Android/Windows/E2E/checks e consistency verdes.
- Próxima tarefa: UC-14 — Markdown: renderização e volta do editor Visual, contra os goldens.


### 2026-10-04 — ChatGPT — UC-14 / Issue #230 / PR #232
- Estado: Markdown e round-trip Visual em implementação na branch `feat/urbe-uc14-markdown-visual`.
- Feito: `MarkdownEngine` porta frontmatter, headings, código, listas, tarefas, tabelas, blockquotes/callouts, wikilinks, links/imagens, ênfase, sanitização e átomos matemáticos; `VisualMarkdown` porta HTML Visual → Markdown com parser BCL próprio.
- Oráculo: testes C# leem diretamente `markdown-golden.json` e `visual-golden.json`; a primeira rodada portátil já passou antes da reconciliação.
- Correções de port encontradas pelo CI anterior: escape HTML ajustado para o comportamento exato do JS (somente & < > aspas), preservando Unicode literal.
- Limites: caret, seleção, toolbar, IME, autocomplete e UI ficam para UC-18; KaTeX/UI matemática fica para UC-16.

### 2026-10-06 — ChatGPT — UC-14 reconciliada com a main
- PR #232 foi reconciliado semanticamente sobre `main@58a78d417259` após o corte P4-9/DEC-0039, sem carregar versões antigas dos documentos.
- Os três arquivos de domínio/teste do UC-14 foram preservados; README/ROADMAP/AGENTSCHAT e handoff foram reaplicados sobre o estado atual.
- Estado: verificando novamente consistency, Core, Web, Android, Windows e E2E no estado combinado atual.


### 2026-10-06 — Codex Urbe math — UC-16 / Issue #257
- Estado: primeira fatia de matemática na branch `feat/urbe-uc16-math-editing`, base `510c22197426`.
- Feito: scanner único compartilhado com Markdown, lookup do cursor UTF-16, delimitadores originais, catálogo imutável de comandos/símbolos/modelos, snippets e autocomplete.
- Paridade: corpus suplementar gerado pelo JS congelado com SHA-256, cobrindo código, preços, escapes, Unicode e limites negativos de autocomplete; corrige distinções de whitespace/dígitos/NUL entre .NET e JS.
- Decisão preservada: ADR-0025 não escolhe biblioteca matemática. Nenhuma dependência ou interop adicionada; tipografia/macros/erros/export continuam abertos na UC-16; UI pertence à UC-18.
- Próximo: verificar Core, checks e consistency; abrir PR para CI portátil, Web, Android, Windows e E2E e integração automática rotineira.

### 2026-10-06 — codex-urbe-import — RM-F7-29 / REQ-110 / URBE-261
- Estado: verifying; P4-9 permanece aberto, falha DEVICE de 1.8.3-beta preservada em HO-20261006-urbe-import-incident.
- Feito: fonte Android SAF para arquivos múltiplos/árvore por streams, fonte desktop limitada por tokens, UI Importar → Arquivos/Pasta inteira, inspeção e confirmação, restauração de identidade/metadados, conflito explícito e backup/rollback/recovery binários. Sucessor portátil em Urbe.Core; compatibilidade JS somente para manter o cliente distribuído funcional até UC-31/32.
- Decisões (com fonte): direção explícita do proprietário na Issue #261; arquitetura registrada no ADR-0011 proposto. Revisão pontual do oráculo documentada, baseline anterior preservado e 297 IDs mantidos. Nenhuma autorização presumida de KaTeX interop ou corte C#.
- Evidência: 206 testes Core passaram; 16 testes Java passaram; 18 grupos suplementares executam produção/IO real e incluem interrupção de processo; E2E mobile arquivos/pasta/reabertura e ZIP com IDs passaram. CI do head c247231 e estado combinado deecfdac passou, APK do pipeline identificado nas Issues #261/#166; não são DEVICE. Main avançou com UC-16/DEC-0040; reconciliação preserva ambos os registros e exige novo CI.
- Pendências / bloqueios: PR crítico depende do integrador e autorização real do proprietário; publicação oficial e registro DEVICE exato virão depois, pelo pipeline existente. ZIP selecionado separadamente; conflitos de documentos/assets na origem exigem escolha explícita; cópias divergentes de vault são recusadas sem alteração. Roteiro: docs/validation/urbe/1.8.4-beta-import-device.md (raiz), ainda sem aprovação.
- Próximos passos: concluir verificações, integrar conforme política, publicar candidato e vincular hash real ao portal. UC-15 #249 possui PRs #253/#254 abertos; UC-16 #257 ainda exige investigação tipográfica/licenças/offline/export. M3 continua dependendo de G-C2.

### 2026-10-06 — codex-urbe-import — UC-16 / Issue #257
- Estado: working; investigação de renderer, não adoção nem fechamento da UC.
- Feito: probe reproduzível que compila/executa CSharpMath em diretório temporário, gera SVG e publica Blazor WASM; Chromium renderiza e repete com rede desligada. Corpus: 172 snippets, sete entradas adicionais, 31 fórmulas reais do tutorial. 163 snippets e 30 fórmulas do tutorial renderizados; diagnósticos conhecidos preservados no relatório, sem mascarar rejeições em goldens de compatibilidade.
- Decisões (com fonte): ADR-0025 mantém Blazor WASM/MAUI Hybrid; sem KaTeX interop, sem referência de pacote no produto e sem UI M3. Pacotes e fontes auditados preliminarmente: MIT nos módulos CSharpMath, LGPL-3.0-only nos backends VectSharp e GUST/OFL nas fontes. Nenhuma adoção ou alteração de licença consolidada.
- Pendências / bloqueios: comandos/macros faltantes, equivalência visual, acessibilidade, licenças e hosts Android/Windows ainda requerem trabalho. PRs UC-15 #253/#254 estão abertos; #253 precisa reconciliação, sem sobrescrever o renderer de seu autor. Publicação de import/P4-9 depende do gate crítico do PR #264 e nova DEVICE exata.
- Próximos passos: definir backend C# por ADR fundamentado e PR crítico antes de adoção; ampliar corpus com renderer legado real e corrigir lacunas sem regravar TeX do usuário. Evidência em docs/csharp/UC16-RENDERER-INVESTIGATION.md e math-renderer-observations.json; ferramenta manual csharp/tests/math-renderer-probe.mjs.

### 2026-10-07 — ChatGPT — UC-18 / REQ-007/035/038 / PR #296
- Estado: PR #296 draft, empilhado sobre #289; `verifying`, sem integração nem validação G-C3.
- Feito: proteção `IsLosslessRoundTrip` byte a byte antes de mutações estruturais; salvamento Visual bloqueado na `WorkspaceSession` quando não preservar Markdown; modo Fonte permanece utilizável; Undo/Redo resincronizam os blocos; testes regressivos para sintaxe não canônica.
- Decisões (com fonte): `docs/csharp/ROADMAP.md` UC-18, NN-001/017/018 e `docs/governance/integration-policy.json`; nenhuma alteração de formato persistente ou boundary. Proteção é conservadora: Markdown incompatível com round-trip exato continua editável em Fonte.
- Verificação: CI do PR #296 pendente; `urbe-checks` e `consistency` são autoridade de execução; não declarar pronto antecipadamente.
- Pendências / bloqueios: PR base #289 precisa ser integrado antes do retarget de #296 para `main`; persistência física, split view e Explorer avançado continuam UC-18.
- Próximos passos: validar PR #296, integrar após #289 e só depois avançar ao próximo recorte UC-18.

### 2026-10-07 — ChatGPT — UC-18 modo Dividido (REQ-007/035/038)
- Estado: verifying, PR a abrir sobre #296 (que depende de #289); não integrado.
- Feito: modo Dividido no editor C# usa a mesma WorkspaceSession para Fonte e Visual; ao digitar na Fonte sincroniza visualização e modelo estrutural; ao editar blocos reflete no texto Fonte; undo/redo permanece compartilhado.
- Testes: smoke Web de alternância e ida-e-volta de alterações, inclusive undo, em / e /preview/ online/offline. CI ainda pendente, sem alegação G-C3.
- Decisões: código local ao Urbe, sem formato paralelo, persistência/host, migração ou distribuição; conforme docs/csharp/ROADMAP UC-18, NN-001/017/018.
- Próximos passos: CI, corrigir regressões, integrar somente após a cadeia base; seguir UC-18 Explorer/hosts.

### 2026-10-08 — ChatGPT — UC-18 aviso de sessão volátil C#

- Estado: verifying. Branch `fix/urbe-uc18-session-persistence-warning` empilhada sobre PR #300; não integrada.
- Feito: página inicial torna explícito que a prévia do Editor/Explorer usa sessão de memória ainda sem persistência física do host; exibe contagens reais de arquivos/abas e link direto para Explorer; status UC-17 atualizado.
- Testes: smoke Web valida o aviso e as contagens após criar três notas e usar três abas, incluindo `/`, `/preview/` e cenários offline; CI de PR pendente.
- Invariantes: NN-001/017/018; sem escrita em disco, formato novo, release, alteração de integração ou alegação de gate humano.
- Próximos passos: CI, retarget e integração automática após a cadeia do modo Dividido; conectar os hosts ao vault somente no escopo/autoridade apropriados.

### 2026-10-08 — ChatGPT — UC-18 Explorer: proteção de caminhos

- Estado: `verifying`, PR #286 em draft sobre main; CI reexecutando.
- Fato: `ExplorerEntries` omitira arquivos de `.urbe` mas ainda podia apresentar a pasta interna. Corrigido filtro de pastas.
- Correções: `CreateFolder`, `CreateNote` e `MoveItem` recusam pastas inexistentes, reservadas/internas e componentes de travessia `.`/`..`; movimentação de arquivo/pasta continua sobre a mesma sessão e preserva IDs.
- Decisões: guardas de sessão host-neutral, sem mudanças no formato do vault, storage físico, migração, backup ou publicação; nenhum gate humano declarado aprovado.
- Pendências: validar CI C#/Web/Android/Windows/E2E e consistency, retomar integração automática da UC-18 após verde.

### 2026-10-08 — ChatGPT — UC-18 guarda para editor HTML inline

- Estado: `verifying`; branch `fix/urbe-uc18-visual-html-lossless` empilhada sobre #286.
- Implementado: `VisualMarkdown.IsLosslessEditorRoundTrip` exige que renderização Markdown → HTML → Markdown mantenha exatamente a fonte original antes de permitir mutação rich HTML.
- Aplicado: `WorkspaceSession.CanEditVisualHtml` e `UpdateVisualHtml` rejeitam transformações quando não há garantia de fidelidade; Fonte continua disponível; nenhuma autoridade de documento nova.
- Testes: casos canônicos e não canônicos (sem newline, CRLF, whitespace); CI do PR pendente.
- Limite: não ativa o PR #287, que continua draft até reconciliar sua UI de `contenteditable` com o Editor estrutural e com esta guarda; G-C3 permanece não validado.

### 2026-10-08 — Codex — UC-18 / REQ-091 painel de referência
- Estado: verifying, branch `feat/urbe-uc18-reference-panel`, Issue #281.
- Feito: nota inteira e bloco Visual fixáveis em painel de consulta, múltiplos snapshots, deduplicação, remoção/fechamento, posição sticky mobile e lateral desktop; referências sobrevivem à navegação e são limpas no load do workspace.
- Decisões (fontes): UC-18, REQ-091, SPEC §15.1 e RM-F7-14 como critério de paridade; implementação apenas no cliente C#, respeitando DEC-0025-C. DocumentStore continua autoridade; painel é cópia volátil explícita.
- Testes: regressão Core de snapshots/remoção/paridade documental e Web smoke de edição, undo, troca de nota, mobile e fechamento; execução registrada no handoff.
- Pendências: seleção livre dentro de bloco e rich HTML/bolha ainda não entregues; não fecha REQ-091, UC-18 ou G-C3, não altera persistência ou release.
- Próximos passos: CI combinado e integração rotineira, depois seleção/toolbar coordenada com #287.

### 2026-10-08 — ChatGPT — UC-18 / REQ-093 modelos de nota C#
- Estado: revisão na branch `feat/urbe-uc18-note-templates-req093`, sem integração, CI ou DEVICE presumidos.
- Feito: mecanismo C# puro de campos `{{campo}}`, preservação do Markdown, modelos como notas ordinárias em `Modelos/`; Editor clona a nota em modelo; Explorer cria nova nota com formulário dinâmico, campos repetidos e validação antes de mutação.
- Decisões (fontes): SPEC §15.1 / REQ-093 / roadmap UC-18, com DocumentStore como autoridade única. Nenhum formato persistido, migração, backend, integração de outro Product ou nova release.
- Testes: adicionada suíte unitária de placeholders; execução .NET local indisponível neste ambiente, CI `urbe-checks`/consistency e smoke Web ainda devem rodar no PR e estado combinado.
- Limites: sessão host-neutral em memória; persistência física futura, Android/toque real, e gate G-C3 seguem pendentes. Não marcar REQ-093 concluído.
- Próximos passos: validar CI do PR, corrigir regressões, integrar somente pelo integrador automático e seguir com a persistência física da UC-18.
