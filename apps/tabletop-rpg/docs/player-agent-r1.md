# RPG-011 — Player Agent headless

**Status:** incremento em revisão; depende da integração de RPG-010 (PR #506).

## Fluxo de decisão

1. `AgentTurnRunner.ReactAsync(participantId, wake)` recebe um gatilho explícito e cria `Campaign.CreatePlayerView(participantId)`.
2. `PlayerAgentDecisionSource` valida que a perspectiva pertence ao `ParticipantId` do perfil e que o assento é Player; seleciona exclusivamente a visão do `CharacterId` do perfil, mesmo quando o participante controla mais de uma personagem.
3. O contexto serializado contém persona, objetivos, relações **declaradas para aquela personagem**, atributos, crenças/conhecimento e memórias subjetivas **da perspectiva existente**. Não recebe `Campaign`, `WorldFacts`, outros personagens nem estado secreto.
4. O Product-local `IModelGateway` gera texto apenas após `turn-started`, `addressed`, `scene-changed` ou `consequence`. Sinais inventados de polling/idle são recusados antes de qualquer chamada.
5. `PlayerIntentDecoder` exige JSON estrito, com somente:
   - `{"kind":"speak","text":"..."}`;
   - `{"kind":"end_turn"}`;
   - `{"kind":"pass"}` (nenhuma ação).
6. `ParticipantId` e `CharacterId` são impostos pelo runtime. Identidades, ações arbitrárias, dificuldade de teste, resultados e campos inesperados na resposta são recusados. O `AgentTurnRunner` delega ao mesmo `SessionEngine` usado para humanos, que executa ou rejeita a intent conforme as regras.

## Responsabilidades e limites

- `PlayerAgentProfile`: persona, objetivos e relações estáticas, em snapshot local. Não é uma segunda base de fatos nem de memória. Conhecimento e memória pertencem ao `Campaign` (inclusive após save/restore v2) e são projetados pela `PlayerView`.
- Sem novo schema, migração de dados do usuário, provider HTTP, SDK de modelo, loops, timers, rede, UI, gerência de orçamento ou história gerada por Mestre.
- **Limite transitório:** RPG-011 ainda não persiste perfis de personalidade/objetivos/relacionamentos. A definição durável desses dados exige uma etapa de schema aprovada com migração segura; não declarar campanha de IA retomável integralmente antes disso.
- **Limite de ações:** ataques e skill checks não são gerados livremente pelo modelo. Ações táticas dependem de visibilidade do alvo, autoridade do Mestre e contratos de seleção de dificuldade previstos no roadmap; isso evita ataques a IDs secretos e DC arbitrária. `end_turn` só funciona se houver encontro válido.
- O contrato é exercitável com um `IModelGateway` simulado. Ainda não há adaptador externo real; não prometer IA jogável em Android.
- `PlayerView` é a fronteira canônica de autorização do contexto. Instruções maliciosas dentro de crenças/memórias continuam sendo texto potencialmente não confiável; nenhum comando delas é autorizado diretamente.
- Testes: conhecimento não revelado não entra no prompt, personagem secundária do mesmo controlador não vaza, saída injetada/malformada falha sem mutar campanha, ações válidas respeitam SessionEngine, sem polling e cancelamento.

## Verificação

```sh
cd apps/tabletop-rpg
dotnet test --project tests/TabletopRpg.Core.Tests
```

Etapas seguintes: RPG-012 Mestre IA e RPG-015 orquestração, com a validação e os contratos necessários. RPG-011 permanece `[~]` até integração e verificações conclusivas.
