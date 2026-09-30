# ADR-0006 — Product Shell, Context e distribuição independente

## Status

Aceito — ratificado pelo proprietário em DEC-0001 ([ADD-0003](../governance/addenda/ADD-0003-aprovacao-de-decisoes-e-superficie-de-decisoes-no-portal.md), 2026-09-30). Os conceitos e NN-023 já haviam sido decididos em DEC-0006.

## Contexto

Antes de iniciar a migração de Lunet2D e Urbe, o proprietário decidiu que a arquitetura passa a ter explicitamente três níveis de experiência (Hub → Product Shell → Workspace/Tool), que cada Product é distribuível de forma independente do Hub, que o Hub pode permanecer privado e que o Ecosystem deve suportar uma plataforma first-party de distribuição e comércio. Esses pontos aparecem em MANIFEST §5, §6, §7 e NN-003 apenas de forma parcial: NN-003 protege o *funcionamento* sem o Hub, mas nada garantia independência de *distribuição*, e o conceito de Product Shell/Context/Distribution Profile não existia.

## Problema

Formalizar esses conceitos de modo que (1) qualquer agente novo os entenda sem depender da conversa (NN-009); (2) a nova regra entre no manifesto sem enfraquecer nada existente; (3) a migração Fase 1 preserve o comportamento atual e não execute a arquitetura futura como efeito colateral (NN-013); (4) não se criem abstrações sem consumidor (NN-020, NN-022).

## Opções

1. **Formalizar agora apenas conceitos, invariante, documentação, roadmap e inventário; implementar nada.**
2. Formalizar e já criar Product Shells, schema de Distribution Profile e metadados de disponibilidade.
3. Adiar tudo para depois da migração.

## Decisão

Opção 1, conforme ADD-0002.

**Decisões do proprietário (DEC-0006):** níveis Hub → Product Shell → Workspace/Tool; Product Shell como Host especializado de um Product, distinto do Hub; Hub privado como cenário suportado; arquitetura ≠ distribuição; Distribution Profile como conceito; Context hierárquico como conceito; Connections é UX sobre Capabilities; visão do Lunet2D como plataforma; Store ≠ Library; Assets como categoria do catálogo; plataforma first-party; Services compartilhados com experiência própria; Urbe como Product completo com Product Shell próprio; sequência de migração e novo gate da Fase 1; nova invariante de independência de distribuição.

**Formulação do agente, a ratificar:**
- **NN-023** inserida no `MANIFEST.md` logo após NN-022 (sem alterar nem renumerar nenhuma invariante), reproduzida em `AGENTS.md` e mapeada em `enforcement-matrix.json`. Única fiscalização automática hoje: `CHK-BOUNDARIES` (nenhum componente depende de forma obrigatória do Hub, no grafo declarado). Demais mecanismos ficam `planned` com fase. A alteração do manifesto está registrada em [`manifest-changelog.md`](../governance/manifest-changelog.md) conforme MANIFEST §0.
- Os conceitos vivem em `docs/architecture/` ([`product-model.md`](../architecture/product-model.md), [`distribution.md`](../architecture/distribution.md), [`product-vision.md`](../architecture/product-vision.md), [`faq.md`](../architecture/faq.md)); **MANIFEST §6 não foi alterado**.
- Product Shell **não** é um tipo de componente de `ecosystem.json`; nenhum campo de visibilidade, distribuição ou modelo comercial foi adicionado a schemas (formato fica para a Fase 2, por ADR).
- Nenhum diretório, código, Service, Store, Library ou Product Shell foi criado.

**Análise de tensões com o manifesto** (MANIFEST §0, item 1):
- §2 ("não se tornar um superaplicativo"): cada Product Shell é independente; sem monólito.
- §5.1 (Lunet continua produto especializado): a visão amplia o ciclo de vida do produto dentro de jogos 2D.
- §47 (não criar marketplace, sistema de conta, backend obrigatório no início): nada é implementado; requisitos decorrentes exigem que o domínio essencial nunca dependa de conta, servidor ou rede (§40, §41).
- NN-003/NN-014: complementadas, não alteradas.
- Nenhuma contradição encontrada.

## Consequências

- Fase 1 passa a incluir mapa funcional e arquitetural nos inventários, sequência obrigatória (inventariar → importar → … → extrair gradualmente) e um gate adicional: migração sem extração estrutural.
- Decisões concretas (contrato de Context, formato de Distribution Profile, nomes dos eixos de disponibilidade, Services, fonte canônica de catálogo/entitlements) continuam abertas e exigem ADR nas fases indicadas.
- Custo: mais documentação normativa a manter consistente; mitigado por `CHK-AGENTS-NN`, `CHK-ENFORCEMENT-MATRIX` e pelo FAQ apontando para as fontes.
- Risco: confundir visão com estado. Mitigado por marcações explícitas ([Decisão]/[Requisito]/[Derivado]/[Aberto]) e por `product-vision.md` declarar que nada está implementado.

## Alternativas rejeitadas

- **Opção 2:** violaria NN-020/NN-022 (abstração sem consumidor), NN-013 e o texto do proprietário, que proíbe criar agora Shells, schemas definitivos ou Services.
- **Opção 3:** a migração aconteceria sem o mapa funcional e sem o gate, arriscando decisões de importação que tornem a arquitetura futura mais cara.
- **Promover Product Shell/Context para MANIFEST §6 agora:** alteração adicional do manifesto não pedida; permanece possível por decisão registrada.

## Referências

ADD-0002; DEC-0006; MANIFEST §0, §2, §3, §5, §6, §7, §40, §41, §47, §53; NN-003, NN-007, NN-012, NN-013, NN-016, NN-020, NN-022, NN-023; `docs/architecture/product-model.md`, `docs/architecture/distribution.md`.
