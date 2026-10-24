# RPG-014 — NPC runtime (R1)

Este incremento implementa somente o item **RPG-014 — NPC runtime; NPC não é automaticamente Player Agent**, conforme o ROADMAP canônico.

## Fronteira de NPC

`NpcProfile` referencia um **CharacterId que já existe** no `Campaign`, a `SceneId` em que o NPC está presente e um `ParticipantId` de assento GameMaster que **controla explicitamente** aquela personagem. Não cria `Participant` novo, não marca o NPC automaticamente como `ControllerKind.Agent`, não chama `IModelGateway` e não utiliza `IAgentDecisionSource`. O motor de regras e o `SessionEngine` continuam únicos.

Perfil autorado: papel, motivação, disposição `Friendly/Cautious/Hostile`. Estado privado transitório: confiança e alerta limitados a 0–10, versão e cursor de evento; a campanha continua sendo fonte canônica de fatos, personagens e cenas.

## Fluxo em jogo

1. O host cria NPCs que já têm `Character`, cena e participante GM legítimo com controle daquele personagem.
2. `NpcRuntime.Register(profile)` valida referências e limites. NPC não reage a eventos antigos de antes do seu registro.
3. `NpcRuntime.Observe(npcId)` é **chamado explicitamente** para consumir eventos canônicos ordenados. Mudanças de cena são replayadas por sequência: combates fora da cena do NPC não aumentam seu alerta. Combate e resolução de missão podem afetar estados emocionais privados, mas **não** registram novos eventos na campanha.
4. Uma interação explícita `NpcInteraction` contém NPC, participante/character jogador e tipo `Greet/Ask/RequestHelp/Threaten`. O runtime verifica que a pessoa controla aquela personagem e que o NPC está na cena ativa.
5. `Propose` calcula deterministicamente uma `NpcReaction` transitória e privada, com resposta contextual e possíveis efeitos de confiança/alerta. **Não escreve nem publica fala alguma.**
6. O host pode chamar `ApplyApproved` para um único objeto de proposta vigente. A proposta não pode ser clonada, inventada, repetida nem utilizada após a campanha avançar. O código só publica `SpeakIntent` pelo `SessionEngine`, que valida o assento controlador da personagem. Se rejeitado, nada altera o estado privado ou canônico.

As falas nesta fase são determinísticas e curtas para não inventar segredos, resultados ou habilidades. NPC pode resistir a ameaças ou recusar ajuda de acordo com sua disposição e experiências; **não se torna jogador**, não decide dados e não executa ações táticas por conta própria.

## Limites explícitos

- Estado da IA/NPC **transitório**, não salva perfil/confiança/alerta no schema v2. Nenhuma migração ou formato de campanha é alterado.
- Sem provider real, timers, polling, loops de LLM, servidor, multiplayer ou UI Android.
- `NpcRuntime` mantém até 128 NPCs; `GameMasterView` tem janela recente de 24 eventos. Um gap no replay gera erro explícito e exige checkpoint/resync do host; não inventar memória ao retomar.
- Apenas NPCs vinculados a `Character`, `Scene` e GameMaster canônicos, sem identidade paralela de jogador.
- Estado subjetivo do NPC é comportamento efêmero e não deve ser confundido com `KnowledgeClaim` ou `MemoryEntry` persistidos no Core.
- Não implementa roteamento automático de turnos, budgets, retries, provedor ou observabilidade (RPG-015).

## Verificação

```sh
cd apps/tabletop-rpg
dotnet test --project tests/TabletopRpg.Core.Tests
```

Testes: registro/autorização de NPC, ausência de Player Agent automático, interações humanas, diálogo aprovado pelo mesmo SessionEngine, reação a ameaça, confirmação única/replay, proposta forjada, revisão obsoleta, combate dentro/fora da cena, replay truncado e entradas inválidas.

RPG-014 fica em revisão até CI e integração governada após RPG-011 PR #511, RPG-012 PR #517 e RPG-013 PR #519. Não declarar aplicativo jogável ou release.
