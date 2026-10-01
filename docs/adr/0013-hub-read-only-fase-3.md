# ADR-0013 — Hub read-only (Fase 3): tecnologia de UI, plataforma-alvo e fontes de dados

## Status

Aceito — o proprietário decidiu em DEC-0022 (alternativa A, escolhida no portal em 2026-10-01; [registro](../governance/responses/DEC-0022.md)): **Hub Android nativo em C# (`net10.0-android`)**, a mesma pilha do Lunet2D, com a leitura de dados numa biblioteca independente de UI. A parte A (contrato de dados somente leitura) segue como proposta pelo agente e aceita junto.

## Contexto

A Fase 3 do ROADMAP (MANIFEST §46) cria o Hub inicial em C#, **somente leitura**: lê `ecosystem.json`, mostra Lunet2D e Urbe, integra a leitura do GitHub e apresenta Past / Now / Next (MANIFEST §18) com CI, releases, branches, PRs e tarefas. O gate é humano e em aparelho: *no celular, o proprietário abre o Hub e compreende o estado atual dos dois produtos sem abrir o GitHub manualmente* (NN-017).

`ARCHITECTURE.md` §8 lista "tecnologia de UI e plataformas-alvo do Hub (Fase 3)" como **não decidido**. O componente `hub` existe em `ecosystem.json` com `status: planned` e sem diretório. Já existem duas superfícies que projetam estado: o GitHub (fonte técnica) e o portal web (`site/`, projeção, ADR-0005); o Hub **não** deve duplicá-las nem virar a autoridade de nada (NN-001, NN-021): ele consome as fontes.

- FATO: o Lunet2D já compila para Android em C# (`net10.0-android`, `apps/lunet2d/src/Lunet.Android`) e tem CI próprio.
- FATO: o portal já gera uma projeção versionada e validada por schema (`site/data/ecosystem-status.json`, `ecosystem-status.schema.json`) a partir das fontes canônicas.
- FATO: GitHub não é IPC (NN-015); aqui é **leitura de registro de desenvolvimento**, não comunicação entre aplicativos.

## Problema

Qual a menor arquitetura que cumpre o gate da Fase 3 sem criar segunda fonte de estado, sem tornar o Hub dependência dos produtos (NN-003, NN-023) e sem abstração compartilhada prematura (NN-020, NN-022)?

## Opções

**A. Decisão de dados (proposta do agente; detalhe local, não depende do proprietário):**

1. O Hub lê, nesta ordem: (a) `ecosystem.json` e documentos canônicos do repositório; (b) a projeção `ecosystem-status.json` publicada pelo portal; (c) a API pública de leitura do GitHub (CI, releases, branches, PRs, Issues). Tudo somente leitura; nada é gravado de volta.
2. Todo dado sem fonte disponível aparece como `not-available`, nunca inventado; offline mostra o último estado conhecido **marcado como tal**.
3. **Past / Now / Next é derivado**, nunca digitado: Past = fases/gates concluídos, releases, decisões decididas, validações; Now = Issues abertas com `state:`, branches, PRs, CI, decisões e validações pendentes; Next = itens `[ ]` do ROADMAP, gates e decisões necessárias. O Hub **não fabrica o Next** (MANIFEST §18).
4. Leitura de rede exige a permissão `network.access` declarada no manifest (NN-016); nenhum token é embutido (MANIFEST §30.2).

**B. Tecnologia de UI e plataforma (decisão do proprietário — DEC-0022):**

1. **Android nativo em C# (`net10.0-android`)**, a mesma pilha já usada pelo Lunet2D; só Android.
2. **UI C# multiplataforma** (.NET MAUI ou Avalonia): Android primeiro, abrindo caminho para desktop.
3. **Web/PWA em C# (Blazor WebAssembly)** servido como site estático: abre no celular sem instalar.

## Decisão

**B1 — Android nativo em C#** (DEC-0022-A). Registro da proposta que precedeu a decisão: o agente propôs a opção A integralmente (é detalhe de implementação dentro de contratos existentes, MANIFEST §22.4) e recomenda **B1** como ponto de partida, com a leitura de dados em biblioteca C# **independente de UI** dentro de `apps/hub` — de modo que trocar a camada de UI depois não reescreva a leitura. Isso é INFERÊNCIA e PROPOSTA, não decisão: B1 minimiza novas pilhas e CI (NN-020); B2 compra desktop e iOS ao custo de uma pilha nova; B3 sobrepõe-se ao portal (ADD-0001) e não atende às fases 4–5 (instalar/abrir apps, IPC) sem outra camada.

## Consequências

- P3-3 cria `apps/hub` (um único projeto de início; nenhum `platform/`, nenhum Service, nenhum componente compartilhado — ADR-0011), `status` do `hub` sai de `planned`, e `CHK-BOUNDARIES`/`CHK-ARCH-REFS` passam a fiscalizar o Hub real: nenhum Product pode depender dele (NN-003).
- O Hub é Product independente com versão própria (NN-014); `version.authority` é decidido em P3-3.
- Se o Hub vier a precisar do Registry, ele é consumidor real e a promoção segue Extraction Review (ADR-0011) — não é automática.

## Alternativas rejeitadas

- **Hub como segunda fonte de estado** (guardar tarefas/estado próprio): viola NN-001 e NN-021.
- **Hub como pré-requisito dos produtos:** viola NN-003 e NN-023.
- **Escrever no GitHub a partir do Hub nesta fase:** fora do escopo "read-only"; exige permissões e ADR próprios.

## Referências

MANIFEST §5.3, §18, §46; NN-001, NN-003, NN-011, NN-014, NN-015, NN-016, NN-017, NN-020, NN-021, NN-022, NN-023; ADR-0005, ADR-0011, ADR-0012; ADD-0001; `ARCHITECTURE.md` §6.1 e §8; DEC-0022.
