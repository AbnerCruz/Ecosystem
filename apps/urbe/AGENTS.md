# AGENTS.md — Contrato para agentes de IA no repositório Urbe

Este arquivo vale para qualquer agente (Claude Code, Codex, Copilot etc.) que trabalhe neste repositório. Ele complementa `CONTRIBUTING.md` e não substitui os documentos normativos em `docs/v2/`.

## 1. Fontes de verdade (em ordem)
1. `docs/v2/REQUIREMENTS.md` — Requirement Ledger (IDs REQ estáveis).
2. `docs/v2/SPEC.md` — especificação normativa da 2.0.
3. `docs/v2/ROADMAP.md` — itens executáveis (`RM-Fn-nn`), com dependências, testes e gates.
4. `docs/v2/TRACEABILITY.md` — gerado; **não editar à mão** (`node tools/gen-traceability.mjs`).
5. `docs/v2/adr/` — decisões arquiteturais; `docs/v2/discovery/` — evidência do estado 1.x.
6. `AGENTSCHAT.md` — log de coordenação e handoff (leia antes de começar, escreva ao terminar).

PRs, commits e CHANGELOG registram trabalho; **não** substituem requisitos, SPEC ou ADR.

## 2. Processo
`IDEIA → DESCOBERTA → DEFINIÇÃO → DECISÕES → REQUISITOS → ARQUITETURA → RISCOS → ESTRATÉGIA → SPEC → ROADMAP → AUDITORIA → EXECUÇÃO`.
Na execução, cada item segue: `IMPLEMENTAR → TESTAR → VERIFICAR → CORRIGIR → DOCUMENTAR → AUDITAR → VALIDAR GATE → AVANÇAR`.

## 3. Regras invioláveis
- Não implemente nada sem um item do ROADMAP (ou bug crítico da 1.x, registrado). Cite o `RM-id` e o REQ no commit/PR.
- **Nunca remova ou enfraqueça um REQ.** Mudar de estado exige justificativa e fica no ledger; requisitos novos são acrescentados.
- Decisão estrutural (boundary, formato de dados, segurança, release, plataforma) exige ADR. Decisão que depende do proprietário: apresente contexto, alternativas, consequências, recomendação e pergunta objetiva — **não decida por conveniência**.
- Separe sempre **FATO OBSERVADO** (com `caminho:linha`), **INFERÊNCIA**, **DECISÃO PROPOSTA** e **DECISÃO CONSOLIDADA**. Não afirme estado do repositório sem tê-lo verificado.
- **Uma autoridade por vez:** extrair código de `app.js` = provar a substituta com teste de produção e **apagar** a implementação antiga no mesmo item. Nada de nova camada de versão, `V21_VERSION` extra, wrapper histórico ou "TODO marcado como feito" (`tools/check-debt.mjs`, quando existir, bloqueia).
- **Dados do usuário são invariante:** local-first/offline-first; nenhum leitor sobrescreve/descarta arquivo de versão desconhecida; migração só com backup restaurável e idempotente (REQ-007/035/038).
- Não altere formato persistido sem linha em `discovery/DATA-CATALOG.md`, política de migração, fixture e teste.
- Não publique versão por efeito colateral: release só por tag/dispatch aprovado (REQ-066).
- **Integração (ADD-0012 e ADR-0015 do Ecosystem):** não integre à mão; quem integra é o integrador automático do Ecosystem. Mudança **rotineira** do Urbe (bug, feature de item autorizado do ROADMAP, refatoração interna, testes, UI, documentação), com `npm run check` e o CI verdes no estado combinado, entra **sozinha**. Mudança **crítica** espera a autorização do proprietário. São críticas as que tocam as zonas críticas do Urbe em `docs/governance/integration-policy.json` do Ecosystem e tudo o que a §5 abaixo lista: formato persistido, migração e backup (`src/persistence/**`, `DATA-CATALOG.md`), fronteira de confiança do desktop, plugins e credenciais de IA, assinatura, identidade e canal de publicação, licença, e mudança de ADR consolidado. Se a sua mudança é crítica fora dessas zonas (ex.: formato persistido dentro do `app.js`), declare-a crítica no handoff do Ecosystem (`criticality`). Nenhum agente põe a label `integrar`.
- Segredos: nunca commitar chaves, tokens ou dados pessoais; chaves de IA nunca vão ao vault, export ou logs.
- Item só vira `[x]` com a Definition of Done (SPEC §10.1) verificada. Se faltar validação humana: `[?]`. Se bloqueado: `[!]` com o bloqueio exato. Nunca comprimir vários requisitos em um item genérico.
- Descobertas novas **aumentam** o ledger/roadmap; nunca o encolhem.

## 4. Fluxo de trabalho
1. Leia `AGENTSCHAT.md` (últimas entradas) e o item do ROADMAP; confirme dependências `[x]`.
2. Crie branch temporária (`feat/REQ-xxx-…`, `fix/…`, `refactor/…`, `docs/…`, `chore/…`) ou use a branch designada pela sessão.
3. Implemente o mínimo do item; rode `npm test` (e os checks aplicáveis: `node tools/check-traceability.mjs`, `node tools/gen-traceability.mjs --check`, `node tools/build-tutorial.mjs --check`).
4. Atualize documentação afetada (SPEC/contratos/tutorial/CHANGELOG) e regenere `TRACEABILITY.md` se ROADMAP/SPEC/REQ mudarem.
5. Commits pequenos e claros; PR com template (problema, REQ/RM-id, o que muda e não muda, impacto em dados/plataformas/performance/segurança, testes, dívida criada/removida).
6. Acrescente entrada em `AGENTSCHAT.md` (formato abaixo) e pare no limite do item.

## 5. O que sempre confirmar com o proprietário
Budgets absolutos de performance (após o baseline); qualquer quebra de compatibilidade de dados; remoção de funcionalidade da 1.x; mudança de qualquer decisão consolidada (ADRs aceitos); publicação fora do mecanismo aprovado (REQ-066). Isto é o que é **crítico** no Urbe; o resto do trabalho dentro do ROADMAP é rotina e não precisa do proprietário (ADD-0012 do Ecosystem).

## 6. Limites
- Fora do escopo da 2.0: reescrita geral, bundler/ES modules (ADR-0001), launcher multi-app, isolamento de plugins, i18n, macOS/Linux, sync/nuvem próprios (`docs/v2/SPEC.md` §13).
- Sem framework/bundler por preferência; sem mover diretórios sem REQ que exija.

## 7. Formato de entrada no `AGENTSCHAT.md`
```
### AAAA-MM-DD — <agente> — <RM-id/REQ ou "descoberta">
- Estado: ...
- Feito: ...
- Decisões (com fonte): ...
- Pendências / bloqueios: ...
- Próximos passos: ...
```
## 8. Ordem do proprietário para Urbe C# — DEC-0042 (2026-10-09)

**DECISÃO CONSOLIDADA — NÃO É PROPOSTA DE AGENTE.** Antes de selecionar ou continuar trabalho no Urbe C#, ler `docs/governance/addenda/ADD-0019-urbe-csharp-sem-retrocompatibilidade-historica-no-beta.md`, DEC-0042 em `docs/governance/decisions.json`, o adendo do Ecosystem ADR-0026 e `docs/csharp/ROADMAP.md`.

O proprietário exige **migração e paridade de funcionalidades** do Urbe JavaScript para C#, mas **dispensa retrocompatibilidade/migração automática do estado de instalações antigas** como requisito de beta e corte C#. Priorizar **filesystem/vault real no Android, Editor/Explorer, Cidade e beta opt-in instalável**, depois concluir as demais funcionalidades. Um vault limpo com cópia manual de `.md` é aceito pelo proprietário. Não abrir tarefas para reconstruir versões antigas, configurações, bancos ou mapas legados a menos que haja defeito concreto com os arquivos Markdown atuais. Código/fixtures de compatibilidade já prontos não precisam ser apagados.

A regra de dados do §3 continua protegendo `.md`, gravações e metadados **atuais**: nada de apagar automaticamente vault legado, fingir que dados em memória foram salvos ou sacrificar segurança de escrita. O JS 1.8.4-beta permanece canal existente enquanto o C# experimental for validado. **DEC-0042 autoriza a direção; PRs críticos ainda seguem o integrador e a label `integrar` aplicada pelo proprietário.** Não declarar gates de release aprovados sem CI/dispositivo quando requeridos.

## 9. Ordem do proprietário — o Urbe nativo sem a simulação visual (DEC-0051, 2026-10-22)

**DECISÃO CONSOLIDADA — NÃO É PROPOSTA DE AGENTE. Prevalece sobre os §10 e §11 onde houver conflito.** Leia `docs/governance/addenda/ADD-0028-urbe-sem-simulacao-visual.md` e o ADR-0034 do Ecosystem antes de trabalhar no cliente C#.

- **A simulação visual saiu do produto:** mundo, cidade, mapa, minimapa, vida, decorações, visual das construções e foco de ligações, com todas as funcionalidades que só existem por eles. Ela está **congelada** no ramo `urbe-simulacao-congelada` (commit `9c13dc2e`). Não reintroduza nenhuma parte dela, nem por paridade com a 1.8.4.
- **O Explorer é a base do app.** Editor e painéis abrem por cima. Na abertura, o app volta para a última nota.
- **Notas:** id, datas e pastas ficam em `.urbe/notas.json` (`NoteLibrary`, `Urbe.Core`). Num vault vindo da cidade, vêm uma vez do `.urbe/mapa.json`. Os arquivos da cidade (`.urbe/mapa.json`, `.urbe/mundo.json`) **nunca são apagados nem reescritos**.
- A interface continua **nativa, sem WebView/HTML/CSS** (Avalonia: `csharp/src/Urbe.Client`, `Urbe.Desktop`, `Urbe.Android`). Regras de domínio só em `Urbe.Core`. A casca Blazor foi retirada (UC-34).
- A 1.8.4-beta (`apps/urbe/`, intocada) segue como referência **fora da simulação**: editor, Explorer, lixeira, histórico, importação, páginas e assistente.
- O substituto da simulação são **conexões calmas** (DEC-0052, `docs/governance/addenda/ADD-0029-urbe-conexoes-no-lugar-da-simulacao.md`): primeiro o UC-71 (conexões da nota aberta), depois o UC-72 (constelação do vault). Nunca a base do app.
- A direção é o Urbe como **produtor de artefatos** sobre o editor Visual (UC-35).

## 10. Ordem do proprietário — o Urbe é a 1.8.4-beta, em C# nativo (DEC-0043, 2026-10-10)

**Histórico na parte da cidade (substituída pelo §9, DEC-0051).** Leia `docs/governance/addenda/ADD-0020-urbe-e-a-1-8-4-nativo-sem-web.md`, o ADR-0032 do Ecosystem e `docs/csharp/PRODUCT-DIRECTION.md` antes de trabalhar no cliente C#.

- A interface C# é **nativa, sem WebView/HTML/CSS**.
- Regras de domínio continuam somente em `Urbe.Core`; o cliente nativo apenas desenha e encaminha entrada.
- Não crie telas sem aprovação do proprietário.

## 11. Mandato de execução de TODOS os agentes Urbe — 10/10/2026

**Ordem expressa do proprietário para TODOS os agentes**, atualizada pelo §9 (DEC-0051): o objetivo é o Urbe nativo em Android/Windows **C#** sobre o `Urbe.Core`, sem a simulação visual. Este mandato vale para desenvolvimento, planejamento, revisão, integrador e qualquer agente futuro.

**Para toda ordem genérica `continue o Urbe`:** ler primeiro `AGENTS.md` da raiz (§1.2), este arquivo, `docs/csharp/ROADMAP.md`, o estado de `main`, Issues, PRs e CI. Avançar uma lacuna concreta do **cliente Avalonia** sem reabrir decisões e sem iniciar UI alternativa.

**Critérios objetivos do produto:**

- acessar o **vault físico** Android com seleção persistente;
- abrir, criar, salvar e reabrir `.md` com verificação real;
- Explorer, editor Visual e Fonte, busca, lixeira, histórico, importação, páginas e assistente funcionando;
- desempenho e experiência de usuário de primeira linha (ADD-0024, ADD-0026).

Cada incremento indica as provas automatizadas e o que ainda exige aparelho físico. **CI verde não é validação no aparelho nem liberação para distribuição.**

**Essência, não defeitos (DEC-0045, 13/10/2026):** os defeitos e melhorias listados em `docs/governance/addenda/ADD-0022-urbe-1-8-4-como-essencia-e-observacoes-de-uso.md` são corrigidos no porte, não reproduzidos. Os itens da cidade estão congelados (DEC-0051).

**Conteúdo dentro do Urbe (ADD-0023/DEC-0046, 14/10/2026):** HTML, PDF, áudio e vídeo do vault e os produtos HTML do projeto são vistos **num painel do próprio Urbe** (`ContentViewers`), nunca mandados a outro app como forma de ver. O visualizador (WebView do sistema isolada, PdfRenderer, player) é conteúdo dentro de painel nativo, não camada de interface.

**Proibições:**

- nova versão Web ou WebView na interface;
- reintroduzir a simulação ou parte dela (DEC-0051);
- duplicar storage ou domínio;
- publicar como beta utilizável uma prévia que não salva dados;
- apagar estado do usuário, inclusive os arquivos da cidade no vault;
- marcar gate humano aprovado por teste headless.

Usar um único Core canônico, preservar a 1.8.4-beta instalada e obedecer às regras de merge/gates do Ecosystem.
