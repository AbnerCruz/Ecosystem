# ADR-0011 — Local-first e promoção por evidência

## Status

Aceito — decisão do proprietário registrada em [ADD-0009](../governance/addenda/ADD-0009-local-first-e-promocao-por-evidencia.md) (2026-10-01). O **mecanismo** de registro (campo `reuse_assessment`, `CHK-HANDOFFS`) foi delegado ao agente e está descrito abaixo. Complementa o [ADR-0009](0009-refatoracao-dos-produtos-pos-migracao.md), NN-020 e NN-022; **não** cria invariante nova nem altera o MANIFEST.

## Contexto

O Ecosystem foi desenhado para que Tools, Services, Workspaces e Libraries possam ser compartilhados (MANIFEST §6, §37). A Fase 1 classificou candidatos (`candidates.md`: Editor como Tool, Git como Service, IA do Urbe como Workspace…). O risco é tratar essa classificação como roteiro de extração e construir abstração compartilhada **preventiva**, antes de existir um segundo consumidor real: contratos congelados cedo demais, custo de versionamento sem benefício (NN-020, NN-022) e dependência do proprietário para classificar arquitetura toda vez que tem uma ideia.

## Problema

Como garantir que (1) uma funcionalidade nova nasça no Product que precisa dela, sem exigir que o proprietário decida a categoria arquitetural; (2) o potencial de reutilização não se perca na memória de uma conversa; (3) um segundo consumidor dispare uma análise — e não uma extração automática; (4) extrações só aconteçam com saldo positivo?

## Opções

1. **Local-first com promoção por evidência**, registrada nos handoffs e fiscalizada por check.
2. **Compartilhar por padrão** tudo que parecer reutilizável (plataforma primeiro).
3. **Sem regra escrita**; cada agente decide caso a caso.

## Decisão

Opção 1.

- **Regra padrão:** uma funcionalidade nova nasce **no Product que tem a necessidade** (por exemplo, `apps/urbe/`), como feature interna, com testes. "Quero X no Urbe" e "Quero Y no Lunet" significam implementar localmente, a menos que já exista evidência concreta de que a responsabilidade é transversal. O proprietário **não** classifica Tool/Service/Library/Workspace/Capability/Core/Shell: isso é do agente e da arquitetura.
- **Extração não corrige "lugar errado"**: é **promoção baseada em evidência** de reutilização.
- **Duplicação temporária é aceitável** e frequentemente preferível a uma abstração compartilhada errada, enquanto o domínio não foi compreendido (Editor, Sprite Studio, Agent Workspace, Git, Docs, UI, autenticação, Store, Library, serviços futuros).
- **Potencial de reutilização** (`reuse_assessment`, opcional, em handoffs de trabalho que cria ou reestrutura funcionalidade): `product-specific` (responsabilidade do Product, nenhum consumidor externo conhecido, nada a fazer), `possible-candidate` (pode haver utilidade fora, sem evidência; **continua local**; fica registrado) e `external-consumer-exists` (há pelo menos um **consumidor concreto** além do dono; abre uma **Extraction Review**, que **não** é extração automática).
- **Extraction Review:** responde às cinco perguntas de NN-022 (complexidade removida, consumidores reais, contrato, custo novo de versionamento/integração, saldo final) e, se positiva, segue: **ADR do Ecosystem → contrato explícito (NN-006) → provider/consumers → extração**, na fase que fornece o contrato (ADR-0009 §X). Resultado registrado em `extraction_review`: `pending`, `ADR-NNNN` ou `declined` (continua local; a justificativa fica em `rationale`). Sem saldo positivo, **continua local**.
- **Detecção de repetição sem inteligência nova:** `CHK-HANDOFFS` falha quando o **mesmo `subject`** é avaliado em dois ou mais componentes e nenhuma avaliação é `external-consumer-exists` — a necessidade repetida não pode passar sem uma Extraction Review registrada. O portal lista os candidatos (derivados dos handoffs).
- **Relação com ADR-0009:** não há terceiro sistema. Fluxo: *nova feature* → nasce no Product; *refatoração interna* → trilho **R**; *potencial de reuso* → `reuse_assessment`; *promoção* → trilho **X**.
- **Não criar infraestrutura compartilhada especulativa:** Identity, Commerce, Catalog, Entitlements, Downloads, Updates, Reviews, Creator Profiles, Notifications, Store/Community backend — sem consumidor real, não existem. Diretórios `platform/`, `tools/`, `workspaces/`, `services/` só nascem com conteúdo real (ADR-0004).
- **Mecanismo de registro (menor complexidade):** campo **opcional** `reuse_assessment[]` no schema de handoff (sem nova versão do schema; handoffs históricos continuam válidos), `{subject, status, rationale, consumers?, extraction_review?}`. Nenhuma nova fonte de verdade: os handoffs são o registro; o portal e `candidates.md` apontam para eles.

## Consequências

- O fluxo do proprietário é "ideia → Product → uso real → evidência → promoção, se fizer sentido", sem burocracia nova para ele; só decisões estruturais com trade-offs voltam ao portal.
- A classificação de `candidates.md` continua válida como **mapa**, não como fila de extração.
- A Fase 2 aplica o princípio a si mesma: o Registry e o validador de compatibilidade nascem **dentro dos checks do Ecosystem** (consumidores reais: checks e gerador do portal) e só serão promovidos a componente próprio quando o Hub (ou outro consumidor real) existir.
- Pode haver duplicação visível entre Urbe e Lunet2D por algum tempo; ela é registrada (`possible-candidate`), não corrigida preventivamente.

## Alternativas rejeitadas

- **Compartilhar por padrão (opção 2):** contratos prematuros, custo de integração sem consumidor (NN-020, NN-022).
- **Sem regra escrita (opção 3):** a decisão depende de memória de conversa (NN-009) e cada agente reinventa o critério.
- **Nova invariante NN-XXX:** desnecessária; NN-020 e NN-022 já fundamentam a regra e o MANIFEST não deve ser alterado sem exigência do processo.

## Referências

ADD-0009; ADR-0004, ADR-0009; MANIFEST §6, §37, §48; NN-005, NN-006, NN-020, NN-022; [`docs/architecture/local-first.md`](../architecture/local-first.md); [`docs/architecture/candidates.md`](../architecture/candidates.md).
