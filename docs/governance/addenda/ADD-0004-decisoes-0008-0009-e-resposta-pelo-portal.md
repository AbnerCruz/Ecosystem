# ADD-0004 — DEC-0008 e DEC-0009 (alternativa A, transitórias) e resposta a decisões pelo portal

> **Tipo:** decisão explícita do proprietário, registrada (MANIFEST §24, nível 1).
> **Autor:** AbnerCruz (proprietário). **Recebido em:** 2026-10-01, em conversa com o agente `claude-code`, e persistido aqui conforme NN-009.
> **Registros de decisão:** DEC-0008 e DEC-0009 (decididas) e DEC-0010 (nova) em [`../decisions.json`](../decisions.json).
>
> **Contexto da mensagem:** resposta ao resumo do agente que apresentava DEC-0008 (canal de releases) e DEC-0009 (publicação do Urbe Web) com alternativas A/B/(C) e recomendação "A" para ambas, visíveis no portal com o inventário como objeto.

## Mensagem do proprietário (transcrição integral)

> Decisão 8 e 9 ambas alternativas A, pode fazer. Quero poder clicar na alternativa no site e ral decisão ser registradas no repositório. Vale ressaltar que essas decisões são para a migração futuramente o próprio ecosystema já declara como deve funcionar.

*(O texto foi mantido como enviado, inclusive "ral" e "registradas".)*

## Interpretação registrada pelo agente (para conferência do proprietário)

Separa o que o texto decide do que o agente inferiu (MANIFEST §53). Uma correção do proprietário é uma nova decisão registrada.

| Trecho | Interpretação adotada | Natureza |
|--------|----------------------|----------|
| "Decisão 8 e 9 ambas alternativas A" | **DEC-0008 = A** (canal de releases por produto nos repositórios de origem; o pipeline do monorepo publica lá) e **DEC-0009 = A** (Urbe Web continua em `AbnerCruz/Urbe`; o pipeline do monorepo publica o site estático no Pages desse repositório). | Decisão do proprietário. |
| "pode fazer" | Aplicar as consequências nos documentos dependentes (migração, ROADMAP, inventários) e prosseguir. **Não** autoriza importar, migrar, refatorar nem alterar os repositórios de origem. | Inferência do agente. |
| "essas decisões são para a migração; futuramente o próprio ecosystema já declara como deve funcionar" | DEC-0008 e DEC-0009 são **transitórias**: valem durante a migração (Fase 1). Depois, o canal de release e a publicação de cada produto passam a ser **declarados pelo próprio Ecosystem** (Distribution Profile, Registry e Launcher — Fases 2 e 4), que as substitui. Por isso **nenhum campo novo** foi adicionado a `ecosystem.json` por causa delas, e o registro marca a decisão como transitória. | Decisão do proprietário (escopo) + inferência do agente (as fases em que o Ecosystem passa a declarar). |
| "Quero poder clicar na alternativa no site e a decisão ser registrada no repositório" | Cada alternativa de uma decisão pendente, no portal, deve ser um controle que leva ao registro da decisão no repositório, sem o agente intermediar. **Capacidade decidida: DEC-0010.** O **mecanismo** não foi escolhido pelo proprietário: o agente propôs [ADR-0007](../../adr/0007-resposta-a-decisoes-pelo-portal.md) e pediu ratificação em DEC-0011. | Decisão do proprietário (capacidade); proposta do agente (mecanismo). |

## Efeitos

- DEC-0008 e DEC-0009 passam a `decided` (A), marcadas como transitórias, com este arquivo como registro. O plano de importação (P1-3) deixa de depender de decisão pendente.
- DEC-0010 cria a capacidade "responder decisões pelo portal". O portal deixa de ser somente leitura **para decisões**, nunca para o resto (ver `docs/architecture/portal.md` §3.2).
- Um agente **nunca** responde uma decisão pendente do proprietário, nem criando a Issue que o portal pré-preenche (MANIFEST §23.2).
