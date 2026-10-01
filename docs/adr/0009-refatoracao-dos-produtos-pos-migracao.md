# ADR-0009 — Refatoração dos produtos depois da migração

## Status

Aceito — ratificado pelo proprietário em DEC-0018 (alternativa A, escolhida no portal em 2026-10-01; [registro](../governance/responses/DEC-0018.md)). Pedido de origem: ADD-0008 ("até finalmente eu poder refatorar os aplicativos para a nova estrutura").

## Contexto

A Fase 1 importou Lunet2D e Urbe sem mudar a arquitetura interna (NN-013, ADD-0002 §17). O passo 10 da sequência obrigatória (`docs/migration/README.md` §7) — extrair/modernizar gradualmente — só começa depois da classificação de candidatos (P1-12) e diz apenas "cada extração com ADR". Falta dizer **como** se refatora um produto para os papéis do modelo de produto (Product Core, Product Shell, Adapter…) sem violar NN-002, NN-005, NN-011, NN-014, NN-020 e NN-022, e **quem** decide cada caso.

Os dois produtos já têm governança própria, viva dentro de `apps/<id>/`: Lunet2D (`docs/adr/0001…0006`, `ROADMAP.md`, `AGENTS.md`, testes de arquitetura) e Urbe (`docs/v2/` com SPEC, ROADMAP, REQ, ADRs `0000…0009`, `check-modules` e lista fechada de exceções de boundary). MANIFEST §26: regra local complementa a do Ecosystem e não a contradiz.

## Problema

Como permitir que o proprietário refatore os aplicativos para a nova estrutura sem burocracia desnecessária (NN-020) e sem que uma refatoração vire, em silêncio, mudança de boundary, de dados ou extração compartilhada (NN-011, NN-022)?

## Opções

1. **Dois trilhos.** Refatoração **interna** (dentro de `apps/<id>`, sem dependência nova fora do produto) segue o processo do próprio produto, com ADR do produto quando muda a arquitetura dele, mais uma lista de verificação do Ecosystem. **Extração** para o Ecosystem exige ADR do Ecosystem, NN-022 e a fase de contratos correspondente.
2. **Tudo por ADR do Ecosystem.** Toda refatoração estrutural, mesmo interna, passa por ADR e decisão aqui.
3. **Sem regra adicional.** Cada produto decide sozinho, inclusive extrações.

## Decisão

Opção 1 (ratificada em DEC-0018).

- **Refatoração interna (R)** — permitida depois do gate da Fase 1. Regras: (a) fica dentro de `apps/<id>`; (b) nenhuma referência a outro produto ou ao Hub (`CHK-ARCH-REFS`); (c) testes do produto antes e depois, iguais ou melhores; (d) **ADR do produto** quando muda a arquitetura dele, no processo do próprio produto (Lunet: `apps/lunet2d/docs/adr/`; Urbe: `apps/urbe/docs/v2/adr/`), citado no PR com referência qualificada (`Lunet ADR 0007`, `Urbe ADR-0010`, nunca só `ADR-0010`); (e) **mudança de formato de dados do usuário** ou de comportamento relevante do produto exige aprovação do proprietário no portal antes (NN-005, NN-011); (f) o produto continua com versão e release próprios (NN-014); (g) depois do merge, disparar a sincronização da origem (DEC-0017-A).
- **Extração (X)** para `platform/`, `tools/`, `workspaces/` ou Service compartilhado: ADR **do Ecosystem** com as cinco respostas de NN-022, pelo menos um consumidor real com teste, contrato explícito (NN-006) e a fase que fornece esse contrato (Capability/Context: Fase 2; Host API: Fase 5; Agent Runtime: Fase 6; primeira Tool: Fase 7).
- **Continuam proibidos** os itens de ADD-0002 §17 que não forem objeto de ADR próprio (Store, Product Shell compartilhado novo, capabilities improvisadas, packages em massa, reescrita do Urbe em C#).
- **Guia operacional:** [`docs/architecture/refactoring.md`](../architecture/refactoring.md). **Candidatos e ordem sugerida:** [`docs/architecture/candidates.md`](../architecture/candidates.md).

## Consequências

- O proprietário pode refatorar os dois produtos logo após o gate, usando o processo que cada um já tem, sem esperar as Fases 2–7.
- O Ecosystem só entra quando a mudança sai do produto ou toca dados do usuário; isso mantém a plataforma pequena (NN-020) e a governança do produto como autoridade da arquitetura interna (MANIFEST §26).
- Exige disciplina de referência qualificada entre ADRs (R-LUN-5) e de manter `CHK-ARCH-REFS` verde.
- Uma refatoração interna mal feita continua possível; a defesa é a mesma de antes: testes do produto, checks do Ecosystem, revisão e validação em aparelho quando houver UI (NN-017).

## Alternativas rejeitadas

- **Tudo por ADR do Ecosystem (opção 2):** duplicaria a governança que cada produto já tem e travaria refatorações pequenas (NN-020).
- **Sem regra adicional (opção 3):** permitiria extração compartilhada sem NN-022 nem contrato (NN-006, NN-022).

## Referências

ADD-0002 §17, ADD-0008; DEC-0018; MANIFEST §26, §37, §39; NN-002, NN-005, NN-006, NN-011, NN-013, NN-014, NN-017, NN-020, NN-022; [`docs/migration/README.md`](../migration/README.md) §7; P1-12.
