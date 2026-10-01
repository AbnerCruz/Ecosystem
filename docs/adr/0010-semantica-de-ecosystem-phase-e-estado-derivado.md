# ADR-0010 — Semântica de `ecosystem.phase` e deriva de estado entre fontes

## Status

Proposto — a ser decidido pelo proprietário em DEC-0019. Até lá vale a **regra de coerência neutra** descrita abaixo, que não depende da escolha.

## Contexto

`ecosystem.json` tem o campo obrigatório `ecosystem.phase` (padrão `phase-N`; ADR-0002). Nenhum documento define o que ele **significa**: o schema só valida o formato, o portal o exibe como "Fase N" e `CHK-PORTAL` confere que a projeção o repete. Durante a Fase 0 o valor `phase-0` era óbvio; a partir daí ele ficou **desatualizado por três etapas** (gate da Fase 0 aprovado, Fase 1 executada, gate da Fase 1 aprovado) sem que nenhum check notasse, porque nenhum check relaciona esse campo ao ROADMAP, que é a autoridade de fases e gates (DEC-0003; `ARCHITECTURE.md` §5). Uma auditoria externa (2026-10-01) o encontrou como `phase-0` com a Fase 1 concluída.

O mesmo padrão apareceu em outras superfícies: README (`não migrado`), `ARCHITECTURE.md` (`planejado para a Fase 1`), a Issue #6 (`state:review` com a tarefa `[x]`) e itens `planned` da matriz de enforcement de uma fase já concluída. Causa comum: **um estado volátil foi copiado em mais de um lugar e nada relacionava as cópias à fonte** (NN-001: caches e projeções devem declarar sua fonte e ser verificáveis).

## Problema

1. O que `ecosystem.phase` significa? Três leituras razoáveis dão valores diferentes **agora**: (a) a fase com trabalho em andamento; (b) a primeira fase cujo gate ainda não foi aprovado; (c) a última com gate aprovado.
2. Como impedir que uma cópia de estado volátil envelheça sem ser detectada?

## Opções

- **A.** **Remover** `phase` de `ecosystem.json`. A fase existe só no ROADMAP (única autoridade); o gerador do portal a deriva da linha `*Estado do gate:*` e o schema deixa de exigir o campo.
- **B.** `phase` = a **mais alta fase com alguma tarefa iniciada** (`[~]` ou `[x]`) no ROADMAP. Hoje `phase-1`; passa a `phase-2` quando a primeira tarefa da Fase 2 for iniciada. Aprovar um gate **não** muda o valor.
- **C.** `phase` = a **mais baixa fase cujo gate ainda não foi aprovado**. Hoje `phase-2`; avança no instante em que o gate é aprovado, mesmo sem tarefa iniciada na fase seguinte.

## Decisão

Pendente (DEC-0019). **Recomendação: A (remover).** Mantém uma só autoridade (NN-001) em vez de uma cópia verificada e é coerente com a regra "quanto mais volátil a informação, menos lugares devem copiá-la". B e C são verificáveis (o check abaixo já calcula ambos), mas continuam sendo uma cópia.

### Decidido já, independente da escolha (detalhe local de implementação + regra de coerência neutra)

- **Gate no ROADMAP é dado estruturado:** a linha `*Estado do gate:* **aprovado|aguardando|não iniciado**` depois de cada `**Gate:**` (padrão: `não iniciado`). É a autoridade do estado de gate.
- **`CHK-STATE-CONSISTENCY`** (novo, em `tests/consistency/Check.cs`) fiscaliza **relações entre fontes estruturadas**, sem procurar palavras em prosa:
  1. *ROADMAP × gate:* um gate `aprovado` não pode ter itens da fase abertos (`[ ]` ou `[~]`).
  2. *ROADMAP × `ecosystem.phase` (coerência neutra):* `phase-N` só é coerente se **todos os gates das fases < N estão aprovados** e **nenhuma fase > N tem tarefa iniciada**. Vale para B e C (enquanto o campo existir) e já teria acusado `phase-0`.
  3. *ROADMAP × Issues (DEC-0003):* `[x]` exige a Issue da tarefa (título começando pelo ID) fechada e sem `state:` ≠ `done`; `[~]` exige uma Issue aberta com `state:` ≠ `done`. Verificado quando existe um instantâneo das Issues (`ECOSYSTEM_ISSUES_SNAPSHOT`, gerado pelo CI); sem instantâneo, é **não verificado**, nunca aprovado em silêncio.
  4. *`ecosystem.json` × existência:* componente `active`/`migrating` tem o `path` com conteúdo; `planned`/`not-migrated` não existem no repositório (já em `CHK-SINGLE-AUTHORITY`; mantido lá por ownership).
  5. *Decisões × documentos de estado atual:* a seção "Não decidido" de `ARCHITECTURE.md` só pode citar (`DEC-NNNN`) decisões `pending` ou `transitional`; uma decisão `decided` não pode aparecer ali como aberta.
  6. *Matriz de enforcement × gates:* mecanismo `planned` com `phase` de uma fase cujo gate já foi aprovado é plano vencido (implementar, reclassificar ou re-faseá-lo).
  7. *Projeção × fontes (já em `CHK-PORTAL` e `CHK-VALIDATION`):* decisão `decided` não aparece como pendente; build `VALIDATED` não aparece como pendente; mantidas lá por ownership e **cobertas por casos de self-test** explícitos.
- **Documentos descritivos não repetem estado volátil:** README e `ARCHITECTURE.md` apontam para a autoridade; só o que não tem autoridade estruturada fica como revisão documental obrigatória (lista em `docs/governance/state-drift.md`).

## Consequências

- O campo `phase` deixa de poder envelhecer em silêncio, qualquer que seja a semântica escolhida.
- Se A (remover) for escolhida: schema, gerador, `CHK-PORTAL` e `app.js` deixam de ler `phase` de `ecosystem.json` e passam a derivá-la do ROADMAP (mudança pequena, com ADR de substituição desta).
- O ROADMAP ganha uma convenção parseável (uma linha por gate), mas continua markdown legível por humanos.
- `[~]` passa a exigir Issue aberta: tarefas em andamento precisam ter Issue (DEC-0003 já dizia "uma Issue por tarefa em andamento"; agora é fiscalizado).

## Alternativas rejeitadas

- **Escolher a semântica em silêncio** e corrigir só o valor: deixaria dois agentes livres para reinterpretá-la (NN-011).
- **Grep de palavras como "não migrado" em prosa:** frágil e com falsos positivos; só relações estruturadas são fiscalizadas (o resto é revisão documental).
- **Eliminar toda duplicação por geração de documentos:** README/ARCHITECTURE gerados criariam outra máquina para manter; basta que eles não declarem estado volátil.

## Referências

NN-001, NN-011, NN-017, NN-018, NN-021; DEC-0003, DEC-0019; ADR-0002, ADR-0003, ADR-0005; `docs/governance/state-drift.md`; MANIFEST §46.
