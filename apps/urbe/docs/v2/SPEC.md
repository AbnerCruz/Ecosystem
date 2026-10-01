# Urbe 2.0 — Especificação canônica (SPEC)

> Estado: **APROVADA pelo proprietário em 2026-09-30** (gate da descoberta liberado — `discovery/OPEN-DECISIONS.md` §5).
> Base: `discovery/*`, `REQUIREMENTS.md` (REQ-001..109), `adr/0001..0007`, `AUDIT-1X.md`.
> Esta SPEC é normativa: todo REQ em estado IMPLEMENTAR aparece aqui em uma cláusula `- **REQ-nnn**`. A rastreabilidade REQ → SPEC → ROADMAP → teste/gate está em `TRACEABILITY.md` (gerada por `tools/gen-traceability.mjs`).
> Não inicia implementação: o runtime 1.8.2-beta permanece intocado até o ROADMAP ser aprovado.

## 0. Convenções normativas
- **DEVE / NÃO DEVE / DEVERIA** têm o sentido de RFC 2119.
- Cláusula = linha `- **REQ-nnn** — …`. Um REQ tem exatamente uma cláusula primária (nesta SPEC); referências cruzadas usam `(ver REQ-nnn)` sem negrito.
- Evidência do estado atual: `discovery/*` (FATO com caminho:linha). Decisões: `adr/*`.
- Gates: **G0** governança/estrutura/medição · **G1** dados e migração · **G2** arquitetura · **G3** segurança · **G4** plataformas/release/distribuição · **G5** performance · **G6** UX/hardening/release 2.0 (definição no §12).

## 1. Escopo, princípios e invariantes

### 1.1 Escopo
A Urbe 2.0 consolida o produto e a base de software da 1.x: arquitetura com autoridades explícitas, dados compatíveis e migráveis, processo rastreável, qualidade verificável, release controlado e espaço seguro para evoluir cidade, editor/PKM, IA, páginas e plugins. Não é uma reescrita.

### 1.2 Cláusulas de princípio
- **REQ-001** — A arquitetura alvo DEVE ser explícita: cada subsistema declara responsabilidade, serviços providos e dependências permitidas/proibidas (§3.1), mantida em `docs/v2/discovery/ARCHITECTURE-MAP.md` e no manifesto de módulos (§3.2).
- **REQ-002** — Toda mudança relevante DEVE ser rastreável necessidade → REQ → decisão/ADR → item do ROADMAP → teste → gate. PR, commit e CHANGELOG não substituem requisitos.
- **REQ-004** — Documentação normativa, código, testes e releases DEVEM permanecer sincronizados por verificação automatizada sempre que viável (manifesto de módulos, versão única, traceability, `build-tutorial --check`).
- **REQ-005** — Um item só é concluído (`[x]`) com implementação, integração, testes, documentação, aceite e ausência de regressão conhecida (Definition of Done, §10.1).
- **REQ-007** — Local-first e offline-first e a compatibilidade dos dados existentes SÃO invariantes durante toda a migração; nenhum item pode violá-los.
- **REQ-011** — Cidade, editor/PKM, IA, páginas, plugins e UX permanecem no escopo; novas capacidades entram apenas como REQ explícito (§11.1).
- **REQ-012** — Nenhuma feature pode ampliar silenciosamente a dívida (camadas de versão, adapters novos, autoridades duplicadas); PRs declaram dívida criada/removida.
- **REQ-018** — Issues, branches, PRs e ADRs DEVEM ter ciclo de vida definido (§10.4).

## 2. Modelo de processo da SPEC
- **REQ-024** — A SPEC e o ROADMAP só são aprovados após auditoria de cobertura REQ → SPEC → ROADMAP → teste/gate executada por `tools/check-traceability.mjs` (§9.5) e revisão manual (`docs/v2/AUDIT-COVERAGE.md`).

## 3. Arquitetura alvo

### 3.1 Camadas e dependências
```
app ─▶ ui ─▶ features (editor, explorer, world, pages, ai, customize, composition, math, tutorial)
                  ─▶ persistence (WorkspacePersistence + adapters) ─▶ native (contrato UrbeNative: web | electron | android)
                  ─▶ core (Core, DocumentStore, History, Trash, Knowledge, Scheduler, Diagnostics, Keymap) · kit (icons, dialogs)
                                                                                   ─▶ vendor
```
As setas indicam **dependência** (quem aponta usa quem recebe). Uma dependência só pode apontar para a mesma camada ou inferior (`vendor < kit,core,native < persistence < feature < ui < app`); `world` e `pages` não dependem de `ai` (exceto adaptadores `*/ai-tools.js`). Exceções conhecidas: `docs/v2/discovery/BOUNDARY-EXCEPTIONS.md` (lista fechada e decrescente).
- **REQ-003** — `app.js` e código legacy DEVEM perder autoridades funcionais por migração incremental: cada item do ROADMAP substitui **uma** autoridade, prova a substituta com teste de produção e **remove** a implementação anterior no mesmo item; reescrita cega é proibida.
- **REQ-015** — Boundaries (camadas do §3.1, ciclos, dependência sem provedor anterior, arquivo fora do manifesto) DEVEM ser validados automaticamente por `tools/check-modules.mjs` no CI e por `tests/boundaries.mjs`; convenção não basta.
- **REQ-022** — Hotspots fora de `app.js` (`pages/engine.js`, `pages/studio.js`, `world/life.js`, `ai/ui.js`, `pages/free.js`, `customize/panel.js`, `world/pixel-art.js`) DEVEM ser avaliados por responsabilidade e acoplamento (relatório em `docs/v2/discovery/HOTSPOTS.md` produzido pelo item) antes de qualquer extração; extração sem avaliação é proibida.

### 3.2 Registro de módulos e runtime sem build (ADR-0001)
- **REQ-025** — DEVE existir `src/modules.json` com, por módulo: `name`, `file`, `provides[]` (globals `window.Urbe*` e serviços), `requires[]`, `phase` (`boot|core|feature|late`), `platforms[]`. `index.html`, `sw.js` (APP_SHELL) e `tools/build-www.mjs` DEVEM ser gerados ou validados contra ele (`tools/check-modules.mjs`), que falha em: dependência sem provedor anterior na ordem, ciclo, arquivo do manifesto ausente no disco/`index.html`/`sw.js`, arquivo de `src/` fora do manifesto. O runtime NÃO DEVE exigir build.
- **REQ-034** — (ADIADO) ES modules/bundler não fazem parte da 2.0; ver §13.
- **REQ-033** — (ADIADO) Launcher multi-app não faz parte da 2.0; ver §13.

### 3.3 Migração do monólito
- **REQ-026** — `aquarium.renderer`, `aquarium.touch` e `aquarium.roads` DEVEM ser ligados ao runtime de produção (`renderer.configure`, `touch.attach`, `roads.setPath/sync` chamados pelo app real) com teste de paridade; só então `drawGround/Regions/Roads/Trees/Buildings`, `frame` e os handlers `canvas*` duplicados em `app.js` (L6) DEVEM ser removidos.
- **REQ-027** — A superfície do editor (renderMarkdown `app.js:761`, editor Visual, autocompletar de wikilinks, barra de formatação, `setEditorViewMode`) DEVE migrar para `src/editor/*` com API pública (`UrbeEditorModel` + serviço `editor.surface`) e testes de comportamento; `app.js` NÃO DEVE conter regra de edição ao final.
- **REQ-028** — `FS`, `Disco` e `DBK` DEVEM tornar-se adapters (`src/persistence/adapters/{idb,fsa,electron,android}.js`) atrás de um contrato único `{list,read,readBlob,write,writeBlob,remove,createFolder,removeFolder,stat}` com testes de contrato compartilhados; `WorkspacePersistence` DEVE ser a única autoridade de escrita do vault.
- **REQ-029** — As cadeias de sobrescrita (`estadoDesejado`, `abrirCidade`, `drawTrees`, `rebuildRoadNetwork`, `drawRegions`), as 5 funções duplicadas e todas as variáveis "Antes/Base/Old" DEVEM ser eliminadas; `V21_VERSION` NÃO DEVE ser reatribuída; ao final `app.js` contém apenas composição/bootstrap e adapters legacy temporários com prazo (§3.4). Nenhuma nova camada de versão.
- **REQ-030** — Cada adapter L1–L4, L7, L8, L10, L11 (LEGACY-MAP) DEVE ser removido ao cumprir sua condição de remoção e depois de existir teste de produção da autoridade substituta; L9 (migrações 1.x) permanece até a política do §5. Remover requer zero consumidores por grep e por teste.
- **REQ-031** — `src/legacy/bootstrap.js` (JSZip) DEVE mover-se para `vendor/jszip/jszip.min.js` com `LICENSE`; `src/legacy/` DEVE conter somente adapters reais; `index.html`, `sw.js`, `modules.json`, `tests/consistency.mjs` atualizados.

### 3.4 Contratos públicos versionados
- **REQ-032** — Os contratos consumidos por terceiros DEVEM ser versionados em `docs/v2/contracts/*.md` e expostos em runtime: (a) API de plugins `urbe.*` (`urbe.apiVersion`, `urbe.plugin({nome,versao,api})`), (b) serviço `world.custom`, (c) comandos e eventos estáveis, (d) `UrbeNative` (§6.2). Mudança incompatível exige nova `apiVersion`, deprecação de uma versão e teste de conformidade.

### 3.5 Prazos de legacy [P]
Todos os adapters legacy remanescentes no fim da fase F2 DEVEM constar em `docs/v2/discovery/LEGACY-MAP.md` com condição e versão de remoção; nenhum sobrevive à 2.0 final sem justificativa registrada.

## 4. Estrutura física
Sem movimentos em massa (OD-07). Diretórios novos previstos: `src/persistence/adapters/`, `src/modules.json`, `vendor/jszip/`, `tests/fixtures/vaults/`, `tests/e2e/`, `tools/` (checks e harness), `docs/v2/contracts/`, `docs/v2/perf/`, `docs/v2/manual/`. Qualquer outro movimento acompanha o REQ que o exige.

## 5. Dados e compatibilidade (ADR-0004, ADR-0006)

### 5.1 Contrato de compatibilidade
- **REQ-023** — Todos os formatos persistidos do vault catalogados em `DATA-CATALOG.md` DEVEM ter: schema, versão, política de migração, fixture e teste; nenhum formato novo entra sem linha no catálogo.
- **REQ-007** (ver §1.2) e **REQ-082** (ver §10.5) complementam.
- **REQ-036** — O vault DEVE declarar `.urbe/vault.json` `{ "formatVersion": 2, "createdBy": "<app>@<versão>", "lastWriter": "<app>@<versão>", "migrations": [{"id","from","to","at","backup"}] }`, lido antes de qualquer escrita; vault sem o arquivo é tratado como 1.x.
- **REQ-035** — Nenhum leitor DEVE sobrescrever, descartar ou apagar arquivo `.urbe/*` cuja versão seja desconhecida ou maior; DEVE preservar o arquivo, avisar o usuário e operar em modo seguro (somente leitura) para aquele artefato. Vale para journal, history, trash, compositions, mapa, tema, páginas, blocos, modelos. Para artefatos que a 1.x sobrescreve, a 2.x grava arquivos paralelos `*.v2.json` mantendo o v1 intacto durante a beta (ADR-0004, OD-11).
- **REQ-038** — Toda migração de formato DEVE criar backup restaurável em `.urbe/backup/<AAAAMMDD-HHMMSS>-<de>-<para>/` (cópia dos arquivos afetados) antes de escrever, ser idempotente e registrar-se em `vault.json.migrations`; o app oferece "Restaurar backup".
- **REQ-037** — DEVE existir `tests/fixtures/vaults/` com no mínimo: `v1-mapa-v1` (IDB legado exportado), `v1-mapa-v2`, `v1-mapa-v4`, `v1-notas-sem-id`, `v1-journal-pendente`, `v1-cidades-mescladas` (com `Cidades/` e `.urbe/origens/`), `v1-personalizacao` (tema, estilos, texturas, plugins), `v1-paginas`, `v1-composicoes-historico`, `v1-mundo-antigo`, `futuro-desconhecido` (versões maiores); e `tests/vault-migration.mjs` que abre cada fixture e afirma: nenhum byte de nota alterado, IDs preservados, arquivos futuros intactos, migração idempotente.

### 5.2 Modelo de artefatos e identidade
- **REQ-014** — O vault DEVE ter um modelo explícito de tipos de artefato (nota, página, modelo/bloco de página, plugin, tema, estilo, textura, composição, asset, sistema) sem perder a propriedade de arquivos comuns: "tudo é arquivo", mas nem todo arquivo textual é nota.
- **REQ-039** — Uma fonte única `src/core/artifacts.js` (`UrbeArtifacts.classify(path) → {type, editable, indexed, textual}`) DEVE substituir as cinco listas de extensões (`workspace.js:9,45`, `app.js:3105-3106`, `app.js:4472`, `ai/tools.js:39`, `native/bridge.js:44`); `.canvas` e demais extensões têm decisão explícita; artefatos de sistema/personalização NÃO DEVEM entrar como nota; `document.open` roteia por tipo.
- **REQ-013** — Relações e metadados internos novos DEVEM usar IDs estáveis; leitura de referências por path/nome da 1.x DEVE ser preservada.
- **REQ-041** — Regiões (`regioes[].id`), construções/assets (`construcoes[].id`) e vínculos (`parentId` ao lado de `parentNoteName`) DEVEM receber IDs estáveis (`reg_…`, `ast_…`) no mapa 2.x; o leitor 2.x aceita mapas sem ID e os gera na migração.
- **REQ-042** — DEVE existir `.urbe/identity.json` `{version:1, docs:{<docId>:{path,fingerprint,seen}}}`; `syncFromDisk` DEVE reconciliar por path e, na ausência, por `fingerprint` (hash de conteúdo normalizado) para detectar rename/move externo, preservando ID; órfãos em history/trash/compositions DEVEM ser coletados (GC) com retenção configurável e registro; o app NÃO DEVE gravar IDs dentro das notas por padrão (frontmatter ADIADO).
- **REQ-040** — `.urbe/mapa.json` DEVE ter um único escritor (`WorkspacePersistence`); `rodarSinc` DEVE ser eliminado; `mapa.v` DEVE ser lido e validado (`v:4` legível pela 1.x); mapa de versão maior segue REQ-035.
- **REQ-043** — DEVE existir versão de mundo explícita (`mundo`) e migração de terreno/layout que faça backup (REQ-038) e ofereça **desfazer** antes de reposicionar a cidade; vaults mesclados sem `mundo` DEVEM ser tratados por regra documentada; `urbeReorganizarCidade` NÃO DEVE mutar sem backup.
- **REQ-044** — O ZIP exportado DEVE conter `urbe-export.json` `{format, appVersion, exportedAt, files:[{path,sha256,size}], state}`; `state` inclui aprovações de plugins e preferências (`urbe.explorer.v2`, `urbe.editor.workspace.v1` por vault), NUNCA chaves de IA; o import valida hashes.
- **REQ-045** — A migração multi-cidade DEVE ser idempotente e terminar: `Cidades/` e `.urbe/origens/` recebem marcador de conclusão em `vault.json`, não são recopiados a cada boot, e o usuário pode arquivá-los.
- **REQ-046** — Na web/FSA, a escrita DEVE ser recuperável (escrever em arquivo temporário e substituir, quando suportado; caso contrário journal por arquivo); o modo IDB interno DEVE oferecer exportação garantida, verificação de integridade e aviso persistente.
- **REQ-047** — Estado local que referencia documentos (`urbe.editor.workspace.v1`, favoritos/recentes, aprovações de plugins) DEVE ser chaveado por vault e tolerar IDs órfãos sem erro.
- **REQ-048** — Campos persistidos sem consumidor (`aiLocal`) DEVEM ser auditados: consumir ou remover, com compatibilidade de leitura e nota no catálogo.
- **REQ-049** — `pages/engine.normalize` DEVE preservar chaves desconhecidas e NÃO DEVE reescrever `version` para o valor corrente; abrir e salvar uma página de versão maior segue REQ-035.

## 6. Plataformas
### 6.1 Princípio
- **REQ-008** — Web, Windows e Android DEVEM compartilhar a aplicação; diferenças ficam atrás do contrato `UrbeNative` e de adapters de persistência (§3.3, §6.2).

### 6.2 Contrato `UrbeNative`
- **REQ-076** — `UrbeNative` DEVE expor `contract:{version:1, capabilities:[…]}` e as capacidades `fs`, `vault`, `openExternal`, `saveFile`, `print`, `update`, `back`, `storageStatus`; cada adapter (Electron `preload.js`, Android `bridge.js`, web) DEVE passar `tests/native-contract.mjs` e declarar capacidades ausentes explicitamente.
- **REQ-079** — Exportação/download DEVE ter paridade: Electron ganha `saveFile` equivalente (diálogo nativo de salvar), Android mantém MediaStore, web mantém download; teste por adapter.
- **REQ-077 / REQ-078** — (ADIADOS) ver §13.

## 7. Segurança (THREAT-MODEL, ADR-0002, ADR-0007)
### 7.1 Princípios
- **REQ-010** — Plugins, IA, filesystem, HTML/conteúdo ativo, bridges nativas, updater e links externos DEVEM ter trust boundaries documentados em `THREAT-MODEL.md` (B1–B8) e propriedades de segurança testáveis; toda nova superfície adiciona linha ao modelo.
- **REQ-021** — O armazenamento e a exposição de credenciais de IA DEVEM seguir o threat model (§7.4) e a política da plataforma.
### 7.2 Plugins
- **REQ-020** — O modelo de confiança de plugins é **full-trust aprovado** (ADR-0002); documentação, UI e testes DEVEM descrevê-lo sem sugerir isolamento.
- **REQ-051** — A aprovação DEVE mostrar código (ou diff em mudança), hash SHA-256 e alcance real (rede, chaves, filesystem, bridge); NÃO DEVE existir fallback FNV (sem `crypto.subtle` o plugin não roda); plugins aprovados por FNV DEVEM ser reaprovados; `api.ia.ferramenta` com escrita DEVE exigir aprovação.
- **REQ-052** — (ADIADO) ver §13.
### 7.3 Plataformas nativas
- **REQ-050** — CSP DEVE ser aplicada e testada: web/PWA (meta ou cabeçalho), Electron (cabeçalho no protocolo `app://`), Capacitor (meta); `default-src 'self'`, `script-src 'self'` (sem `unsafe-inline`; o script inline de aparência DEVE ser externalizado), `frame-src` restrito ao necessário, `connect-src` para os provedores de IA configurados e GitHub; plugins aprovados via `new Function` exigem `'unsafe-eval'` **[decisão explícita: aceitar `unsafe-eval` somente enquanto plugins full-trust existirem; documentado]**.
- **REQ-055** — Electron DEVE: conceder permissões só à origem `app://urbe` e a lista mínima; validar host no `protocol.handle('app')` com allowlist de arquivos servidos; comparar origem do IPC por igualdade exata; imprimir PDF sem `file://` com privilégios (página em `app://` ou `data:` em sessão isolada); tornar `URBE_TEST_*` inertes fora de `NODE_ENV=test`/flag de build de teste.
- **REQ-056** — Android DEVE: `allowBackup=false`; `printHtml` com JS desabilitado ou base de origem distinta do app; FileProvider limitado às pastas necessárias; validação canônica de caminho no Java para toda operação de arquivo (não só `readTexts`); `listTree` sem seguir symlinks.
- **REQ-057** — Conteúdo ativo: `embed` NÃO DEVE combinar `allow-scripts` com `allow-same-origin`; recursos de CDN (PDF.js, KaTeX, fontes) DEVEM ter versão fixa e SRI ou ser vendorizados; exports HTML autocontidos por padrão; testes de sandbox e SRI.
- **REQ-058** — Importação de ZIP/arquivos DEVE limitar tamanho total, contagem e razão de compressão, rejeitar entradas absolutas, com `..` ou controle, e ter testes de zip-slip/bomb em web, Electron e Android.
- **REQ-059** — (ADIADO) ver §13; até lá `tutorial` e release notes DEVEM documentar SmartScreen/APK de fonte desconhecida (REQ-082).
### 7.4 IA
- **REQ-053** — Chaves de IA DEVEM ser excluídas de export/backup (`allowBackup=false`, REQ-044), mascaradas na UI, com aviso de risco e documentação; a chave NUNCA vai ao vault nem a logs.
- **REQ-054** — (ADIADO) ver §13.
- **REQ-060** — Artefatos ativos criados por IA (`.js`, `.css`, `.html`) DEVEM exigir confirmação explícita com prévia, NÃO DEVEM executar/aplicar automaticamente, e CSS de IA NÃO DEVE requisitar recursos externos sem aprovação; a UI do agente DEVE avisar sobre prompt injection em conteúdo importado; testes cobrem cada política.

## 8. Performance (PERFORMANCE.md)
- **REQ-009** — Performance DEVE ter baselines, budgets, cenários reproduzíveis e gates de regressão (G5).
- **REQ-070** — `tools/perf/` DEVE conter gerador determinístico de vaults `S/M/L` (50/500/5000 notas) e scripts dos cenários de PERFORMANCE §4; resultados versionados em `docs/v2/perf/baseline-<data>.json` com commit e ambiente; executável por `npm run perf`.
- **REQ-071** — Budgets DEVEM ser derivados do baseline e publicados em `docs/v2/perf/budgets.json` (relativos provisórios, absolutos após aprovação do proprietário); `npm run perf:check` DEVE falhar em regressão além da tolerância.
- **REQ-072** — `WorkspacePersistence.flush` DEVE gravar apenas o que mudou (diff por documento e por side-file sujo) com custo independente do tamanho do vault; teste mede bytes e tempo por flush em `S/M/L`.
- **REQ-073** — Quick-open e `KnowledgeIndex.search` DEVEM usar índice pré-normalizado atualizado em `document:*`, debounce de entrada e limite de resultados; custo por consulta independente do tamanho do conteúdo.
- **REQ-074** — Studio, IA, KaTeX e `tutorial/content.js` DEVEM carregar sob demanda (mantendo sem build, via injeção de script declarada no manifesto §3.2); o load do vault DEVE usar concorrência limitada.
- **REQ-075** — `history.json` (e `history.v2.json`) DEVE ter teto de tamanho por documento e total, compactação e gravação incremental.

## 9. Qualidade e testes (TEST-MATRIX.md)
- **REQ-016** — Testes de integração/E2E relevantes DEVEM ser reproduzíveis a partir do repositório e associados aos gates que cobrem (coluna "Gate" no ROADMAP).
- **REQ-061** — `npm run test:e2e` DEVE executar Playwright (web, Chromium) e smoke do Electron sobre um vault fixture: abrir → criar nota → salvar → recarregar → ver na cidade → aprovar plugin → import ZIP → export.
- **REQ-062** — DEVEM existir testes para `native/desktop/main.js`, `preload.js`, `UrbeAndroidPlugin.java` (JUnit) e o Service Worker real.
- **REQ-063** — `npm test` DEVE ser um runner Node (`tools/run-tests.mjs`) multiplataforma que executa todos os `tests/*.mjs`, agrega falhas e imprime resumo.
- **REQ-064** — Testes que fatiam `app.js` ou checam strings DEVEM ser substituídos por testes de comportamento à medida que o código migra; nenhum teste PODE depender de marcadores de texto do monólito ao final da fase F2.
- **REQ-068** — `tools/check-traceability.mjs` DEVE rodar no CI e falhar quando REQ IMPLEMENTAR não estiver em SPEC, ROADMAP e TRACEABILITY.
- **REQ-069** — O CI DEVE construir e iniciar (smoke) o instalador Windows e o APK com um vault fixture.
### 9.5 Verificadores estruturais
`tools/check-modules.mjs`, `tools/check-traceability.mjs`, `tools/version.mjs`, `tools/check-license.mjs` (executados em G0/G4 no CI).

## 10. Processo, release, distribuição e governança
### 10.1 Definition of Done
Item concluído quando: implementado; integrado (sem código morto/duplicado); testes (unitário/contrato + fixture/E2E quando aplicável) passando no CI; documentação atualizada (SPEC/contratos/tutorial/CHANGELOG); critério de aceite do item verificado; gate da fase satisfeito; sem regressão conhecida.
### 10.2 Versão e release
- **REQ-019** — A versão do produto DEVE ter uma única fonte (`package.json`); as demais representações são derivadas ou validadas.
- **REQ-065** — `tools/version.mjs check|sync` DEVE validar/sincronizar `package-lock.json`, `core.js`, `index.html <title>`, `sw.js`, `CHANGELOG`, testes e `V21_VERSION` (que deixa de ser reatribuída); o CI executa `check`.
- **REQ-006** — Integração em `main` e publicação de release DEVEM ter gates separados.
- **REQ-066** — A publicação DEVE ocorrer só por tag `v*` ou `workflow_dispatch`, depender de testes verdes, com `permissions` mínimas, actions fixadas por SHA e Dependabot (`.github/dependabot.yml`); push em `main` NÃO DEVE publicar (ADR-0005).
- **REQ-081** — DEVE haver política documentada (`docs/v2/RELEASE.md`): SemVer com canais `beta`/`stable`, release notes do CHANGELOG, checklist de release, rollback (remoção/pre-release do Release e `latest.yml` anterior; dados via backup REQ-038) e teste de dry-run.
### 10.3 Distribuição e licença
- **REQ-017** — A política de licença e distribuição DEVE ser decidida explicitamente antes da ampliação pública da 2.0 (ADR-0003).
- **REQ-080** — DEVEM existir `LICENSE` (todos os direitos reservados), `THIRD-PARTY-NOTICES` (KaTeX, JSZip, pako, PDF.js, Electron/Chromium, Capacitor, chokidar, electron-updater) e `package.json` `"license":"SEE LICENSE IN LICENSE"`; `tools/check-license.mjs` valida coerência. Licença decidida pelo proprietário (OD-03): todos os direitos reservados.
### 10.4 Governança do repositório
- **REQ-067** — As branches `claude/*` DEVEM ser verificadas (incorporada ou descartada conscientemente, registrado em `AGENTSCHAT.md`) e removidas; trabalho novo nasce de issue/REQ; branches são temporárias (CONTRIBUTING §3).
- **REQ-088** — `AGENTS.md` e `AGENTSCHAT.md` (raiz) DEVEM existir, ser referenciados por CONTRIBUTING e `docs/v2/README.md` e ser atualizados a cada sessão de trabalho.
### 10.5 Migração para usuários
- **REQ-082** — DEVE haver guia 1.x→2.x (`docs/v2/MIGRATION.md` + tutorial + aviso no app) cobrindo leitura total, escrita 2.x com proteção forward, arquivos `*.v2.json`, backup/rollback, reaprovação de plugins, SmartScreen/APK.

## 11. Produto e UX
### 11.1 Regras
- **REQ-083** — Os critérios de sucesso S1–S10 (PRODUCT-UX §6) DEVEM estar publicados em `docs/v2/SUCCESS.md` com verificação e gate associados e ser avaliados antes da release 2.0.
- **REQ-084** — O Tutorial (`tutorial/`) e a documentação de usuário DEVEM cobrir migração, segurança de plugins, backup/restauração e novos fluxos; `tools/build-tutorial.mjs --check` DEVE validar a cobertura por lista de tópicos obrigatórios.
- **REQ-085** — A UI DEVE expor Diagnóstico, Modo seguro e Recuperação (journal, lixeira, versões, backup de migração) em um único lugar acessível (Configurações → Recuperação), documentado.
- **REQ-086** — DEVE existir linha de base de acessibilidade (`docs/v2/A11Y.md`): navegação por teclado nos fluxos essenciais, foco visível, contraste AA nos temas padrão, rótulos ARIA em controles, teste automatizado (axe) no E2E e checklist manual.
- **REQ-087** — (FORA DE ESCOPO) ver §13.

## 12. Gates
| Gate | Nome | Critério de saída |
|---|---|---|
| G0 | Governança, estrutura e medição | `AGENTS.md`/`AGENTSCHAT.md`; `check-modules`, `check-traceability`, `version.mjs check`, `run-tests` no CI; harness de baseline rodando; branches tratadas; release separado |
| G1 | Persistência, dados e migração | fixtures de vaults históricos verdes; proteção forward; `vault.json`; backup; adapters de persistência e mapa com escritor único; identidade; modelo de artefatos |
| G2 | Arquitetura | renderer canônico em produção; editor extraído, cadeias e adapters legacy removidos; legacy zerado ou justificado; sem teste dependente de marcadores de `app.js` |
| G3 | Segurança | CSP; plugins UX; hardening Electron/Android; HTML/embeds; ZIP; IA; testes de propriedade verdes |
| G4 | Plataformas/release/distribuição | E2E web+Electron; smoke instalador/APK; contrato `UrbeNative`; release por tag; `LICENSE` publicado |
| G5 | Performance | baseline publicado; budgets aprovados; `perf:check` verde; persistência incremental e busca indexada |
| G7 | Feedback de uso da beta | bugs OBS reproduzidos em E2E e corrigidos com teste de regressão; itens de feature entregues conforme §15; decisões OD-12..15 registradas em ADR |
| G6 | UX e release 2.0 | critérios S1–S10; acessibilidade; tutorial/migração; recuperação; auditoria final de cobertura; release candidato |

## 13. Adiados, fora de escopo e não objetivos
| REQ | Estado | Justificativa e gate de reavaliação |
|---|---|---|
| REQ-033 | ADIADO | Launcher multi-app: ideia futura do proprietário; reavaliar após 2.0 |
| REQ-034 | ADIADO | ES modules/bundler: proprietário mantém sem build; reavaliar junto de REQ-033 |
| REQ-052 | ADIADO | Isolamento de plugins: full-trust decidido (ADR-0002); reavaliar com contrato de API estável |
| REQ-054 | ADIADO | Cripto nativa de chaves: custo/paridade; reavaliar após E2E nativo |
| REQ-059 | ADIADO | Assinatura de instalador/verificação de updater: certificado/custo; reavaliar ao sair do beta |
| REQ-077 | ADIADO | SAF/pasta no Android: Play Store fora de alvo |
| REQ-078 | ADIADO | macOS/Linux: fora dos alvos declarados |
| REQ-087 | FORA DE ESCOPO | i18n além de pt-BR: sem demanda |
Não objetivos: reescrita geral; nuvem/sync próprio; telemetria; iOS; novas features fora do ledger.

## 14. Decisões e ADRs
ADR-0001 (build), ADR-0002 (plugins), ADR-0003 (licença), ADR-0004 (compat/forward), ADR-0005 (release), ADR-0006 (identidade/artefatos), ADR-0007 (IA/CSP): todas Accepted em 2026-09-30.

## 15. Feedback de uso da beta (F7 — `discovery/FEEDBACK-BETA.md`)
Comportamentos observados pelo proprietário na beta 1.8.2. Cada cláusula é verificada por E2E (Chromium, viewport de desktop e de celular) e/ou teste de simulação; os que dependem de decisão citam o ADR.

### 15.1 Editor
- **REQ-090** — O botão/atalho de código NÃO DEVE inserir texto de reserva. Com seleção: envolve em código em linha (`` ` ``); sem seleção: insere o par de crases com o cursor dentro; o código em linha DEVE funcionar no meio do parágrafo (não ocupa a linha); o bloco de código (``` ) é outro comando. Vale para o modo Visual e o modo Fonte e para a bolha de formatação (`app.js:3965` hoje insere `'código'`).
- **REQ-095** — O estado (ativo/inativo) de cada ferramenta da barra e da bolha DEVE ser derivado da formatação sob o cursor/seleção a cada mudança de seleção (`selectionchange`), nunca de um estado local independente; clicar alterna e o estado visível é sempre igual ao real.
- **REQ-106** — DEVE existir uma pilha de origem da abertura do editor (Explorer, busca, link, cidade, IA); "Voltar" restaura a origem (aba Explorer com a mesma pasta/rolagem), e só volta à cidade quando a origem foi a cidade.
- **REQ-107** — No celular a barra de ferramentas do editor DEVE acompanhar o teclado virtual usando `visualViewport` (posicionada imediatamente acima do teclado), sem cobrir o texto sob o cursor.
- **REQ-109** — Clicar em `[[alvo]]` inexistente DEVE oferecer "Criar nota" (mesma pasta da nota atual, título = alvo) e abri-la; ao digitar `[[Nova]]` e fechar os colchetes o sugestor DEVE oferecer criar; a criação usa o serviço canônico de documentos e é desfeita pela lixeira/histórico.
- **REQ-089** — Comentários: DEVE ser possível anotar um trecho da nota sem alterar o texto do usuário; o armazenamento é um **sidecar** `.urbe/comments.json` (`{version:1,comments:{<docId>:[{id,anchor:{start,end,quote,fingerprint},text,author,created,modified,resolved}]}}`, ADR-0008) que não altera o arquivo da nota; os comentários são preservados na edição externa; na exportação para páginas HTML aparecem como notas marginais ou são omitidos por opção explícita.
- **REQ-091** — "Fixar em painel": selecionar um trecho (ou abrir uma nota) e fixá-lo em um painel fixo (topo no celular, lateral no desktop), com rolagem própria e botão de fechar; o editor continua editável e rolável; o painel pode ter vários trechos e persiste na sessão.
- **REQ-092** — Estilos de tag: `tema.json` ganha `tagStyles` (`{"todo":{"estrutura":"callout","cor":"#…","icone":"…"}}`); linhas começando por `#Tag:` renderizam com o estilo da tag; padrões embutidos (`#Todo`, `#Ideia`, `#Aviso`); o texto continua sendo Markdown puro no arquivo.
- **REQ-093** — Templates: pasta `Modelos/` reservada; "Salvar como template" e "Nova nota a partir de template" com campos `{{campo}}` preenchidos por formulário; o template é uma nota comum.
- **REQ-094** — Timer/cronômetro/relógio no editor (rodapé): iniciar/pausar/zerar; **recurso nativo** (OD-13) no rodapé do editor; sem persistir dados fora do vault/aparelho.

### 15.2 Explorer
- **REQ-096** — A aba hoje chamada "Notas" passa a se chamar **Explorer**; lista todos os tipos de arquivo do vault com nome, extensão e ícone por tipo (SPEC §5.2); tocar/clicar em pasta seleciona e exibe as ações (novo, importar, renomear, excluir, mover); arrastar e soltar (mouse) e toque longo + arrastar (celular) movem arquivos e pastas; testes E2E em viewport de desktop e de celular.

### 15.3 Cidade
- **REQ-097** — A população de moradores só muda por regra explícita (nascimento/chegada/partida documentados); NENHUM morador desaparece por erro de rota/lote/estado; teste de simulação longa determinística (semente fixa, ≥ 20.000 passos) verifica a invariante.
- **REQ-098** — Moradores executam atividades ligadas ao mundo — buscar água (rio/poço), cortar madeira (árvores), plantar/colher (canteiros) — além de ir de casa em casa; cada atividade tem estados observáveis e limites de custo por quadro; pode ser desativada na Personalização.
- **REQ-099** — Eventos: frequência padrão reduzida (intervalo mínimo entre eventos), cada evento desativável na Personalização e o arco-íris desativado por padrão.
- **REQ-100** — No zoom máximo, o nome da pasta de nível raiz é exibido como rótulo do bairro-mãe.
- **REQ-101** — Menu "+": ações distintas e sem duplicidade — Posicionar construção, Desenhar região, Importar, Decorar; tipo/imagem opcionais em cada uma; nova nota é "posicionar construção" com tipo `.md` por padrão.
- **REQ-102** — Contorno dos bairros: a pasta raiz usa muralha; subpastas usam contorno de baixa opacidade; `tema.json` ganha `cidade.contornos` (ativar/desativar) exposto na Personalização; opacidade reduzida por padrão.
- **REQ-103** — Decoração: itens decorativos (árvores, bancos, lampiões, cercas…) posicionáveis e removíveis, persistidos no `mapa.json` (`decoracoes[]`, aditivo) e nunca tratados como nota; catálogo padrão e por textura personalizada.
- **REQ-104** — A construção depende da extensão (sprite por tipo, agrupamento por tipo na distribuição do lote); tocar em uma construção abre a edição do visual (sprite/imagem).

### 15.4 Escopo e dados
- **REQ-105** — Composições: migração automática e reversível (backup, REQ-038) de `.urbe/compositions.json`/`compositions.v2.json` para páginas (`Páginas/…page.json`), preservando fontes, ordem e estilos; depois a UI e os módulos de composição são removidos (REQ-030/029). Aprovado pelo proprietário (OD-14, ADR-0009).
- **REQ-108** — Tutorial: painel dedicado com navegação, busca e visualização de texto/imagens, aberto por Configurações → Tutorial; o conteúdo vem de `src/tutorial/content.js` (sem criar notas/casas); vaults com a pasta `Tutorial/` existente mantêm as notas do usuário (a pasta vira comum e a migração avisa); `tools/build-tutorial.mjs --check` continua validando o conteúdo.
