### 2026-10-04 — Codex — UC-2: atualização e cliente unificado

- FATO OBSERVADO: PR #169 integrado; trabalho sobre 093ef4f inclui a versão-ponte #167.
- Feito: 19 casos portáveis Android/Windows e sete UI, corpus aditivo 259; cliente de referência unificado executa todas as famílias, DOM Visual/UI em Chromium real.
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
- Feito: sete casos `vault.crash-recovery` injetam falha na criação do journal, no segundo arquivo, na remoção de nota e na remoção final do journal. O corpus passa de 205 para 209 casos.
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
- Feito: matriz dos 101 REQ IMPLEMENTAR com fontes/aceite e inventário de testes/tutorial; oráculo SHA-259 incluindo .urbe; 40 casos Markdown e 110 Visual como JSON portável; protocolo de cliente e runner que recusam resultados incompletos/divergentes; adapter JS calcula Markdown real.
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
