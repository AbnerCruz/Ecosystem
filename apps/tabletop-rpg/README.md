# Tabletop RPG

Product do Ecosystem para RPG de mesa com humanos e agentes de IA como participantes de primeira classe.

Identidade estável: `tabletop-rpg`. O nome de exibição é provisório e pode mudar sem alterar o ID.

## Direção

- C# integral para o código novo.
- Mobile-first.
- MVP solo: um humano pode jogar com Mestre e jogadores controlados por agentes.
- Multiplayer é requisito do produto final, mas não pertence ao MVP.
- O motor do jogo, não o modelo, é autoridade de estado, regras e resultados.
- Estado real do mundo, conhecimento de cada personagem e memória subjetiva são domínios separados.
- Agentes recebem visão filtrada e devolvem intents; não recebem uma referência mutável ao estado.
- Local-first e Product independente: nenhum outro Product é requisito de funcionamento ou distribuição.

## Estrutura inicial

- `src/TabletopRpg.Core`: domínio e runtime headless, sem UI, rede ou SDK de provedor.
- `src/TabletopRpg.Persistence`: codec versionado e store local com save atômico, backup e recuperação.
- `src/TabletopRpg.ModelGateway`: contrato local de geração por turno e configurações não secretas de modelo/provedor, sem rede/SDK.
- `src/TabletopRpg.PlayerAgent`: perfil de personagem e decisão por gatilho explícito, com JSON estrito convertido em intents autorizadas.
- `src/TabletopRpg.GameMaster`: Mestre IA com visão privada, propostas tipadas e ferramentas autorizadas apenas após aprovação explícita.
- `src/TabletopRpg.Director`: Director de tensão e fios narrativos com cues privadas, determinísticas e acionadas por eventos; sem mutação de campanha.
- `src/TabletopRpg.Npc`: NPC runtime determinístico com presença por cena, atitude e propostas de diálogo aprovadas pelo SessionEngine; não é Player Agent.
- `tests/TabletopRpg.Core.Tests`: testes de domínio e persistência.
- `SPEC.md`: requisitos do Product.
- `ROADMAP.md`: fases e gates próprios.
- `docs/persistence-v1.md`: formato legado, contratos originais e política de recuperação.
- `docs/persistence-v2.md`: schema atual (RPG-003), mundo/quests/inventário e migração v1 segura.
- `docs/combat-r0.md`: runtime RPG-004, esquema neutro de regras, turnos, intents e limites de persistência.
- `docs/model-gateway-r1.md`: contrato RPG-010, limites de contexto e credenciais fora da campanha.
- `docs/player-agent-r1.md`: RPG-011, perspectiva restrita, parser, integrações e pendências.
- `docs/game-master-r1.md`: RPG-012, visão privada do Mestre, rascunhos de narração/revelação e aprovação no host.
- `docs/director-r1.md`: RPG-013, tensão, ameaças, pistas, promessas, consequências e limites.
- `docs/npc-runtime-r1.md`: RPG-014, NPCs independentes dos jogadores IA e publicação de fala governada.

## Teste

```bash
cd apps/tabletop-rpg
dotnet test --project tests/TabletopRpg.Core.Tests
```

Requer .NET SDK 10+. O gateway R1 define a fronteira de execução por turno, mas o Product ainda não faz chamadas a provedor LLM real.

## Mundo e campanhas (RPG-003)

O Core mantém cenas e missões com IDs estáveis, inventário de personagens, condições e
recursos, com eventos auditáveis e relógio ficcional separado do relógio lógico.
Escrita persistida passa a schema v2; importações v1 mantêm identidade/sessões/fatos
mas iniciam o novo estado de mundo vazio (não havia esses dados no formato antigo).
A transição não autoriza UI, IA, multiplayer nem divulgação automática de informações
secretas na visão de um Player Agent; esses itens têm gates próprios no roadmap.

## Combate R0 (RPG-004)

O `CombatEncounter` gerencia ordem e turnos explícitos com validação de
participantes no mesmo `SessionEngine`, usa `ICombatRules` pluggable e dados
rolados por `IDiceRoller`. Dano reduz o recurso `hp` do alvo na campanha;
ações/eventos/HP sobrevivem ao codec v2. O cursor de turno não é salvo nesta
fase: a sessão de combate deve ser reiniciada explicitamente após retomar
uma campanha. Sem IA real, multiplayer, app mobile ou biblioteca de regras
comercial. RPG-004 depende da integração prévia de RPG-003 (PR #310).

## Gateway de modelos (RPG-010)

`IModelGateway` aceita somente mensagens projetadas pelo chamador e chamadas explícitas. Configuração é separada da campanha e o único vínculo com a credencial é o nome de uma variável de ambiente; segredos nunca entram no schema v2. Saídas de modelo são texto não confiável, sujeito a parsing de intent e autorização no `SessionEngine` em RPG-011. Ver `docs/model-gateway-r1.md`.

## Player Agent (RPG-011)

O novo `PlayerAgentDecisionSource` adapta o Gateway ao `AgentTurnRunner` existente. Usa apenas a perspectiva do personagem designado e decodifica JSON estrito (`speak`, `end_turn` ou `pass`), sem capacidade de escolher identidade, rolagem ou dificuldade. O motor continua sendo a única autoridade sobre ações e eventos. O perfil de persona/objetivos/relações ainda é transitório, sem novo formato de save; não existe provedor real nem sessão mobile com IA nesta fase.

## Mestre IA (RPG-012)

O `GameMasterAgent` consulta uma projeção canônica separada de `PlayerView`, podendo ver fatos secretos do mundo sem transmiti-los automaticamente aos jogadores. Suas respostas são somente propostas: texto narrativo ou revelação de um fato para uma personagem específica. A aplicação requer aprovação independente pelo host, papel de Mestre válido e revisão atual da campanha. O contrato ainda é headless, sem serviço de modelo real ou interface para aprovar propostas.

## Director (RPG-013)

O `DirectorRuntime` consome uma `GameMasterView` somente quando o host o aciona, produz sugestões privadas sobre tensão, ameaças, pistas e promessas, e nunca altera a campanha. Sem roteiro imposto, sem polling, sem chamadas reais a IA ou novo schema de save. Consulte `docs/director-r1.md`.

## NPC runtime (RPG-014)

NPCs são personagens canônicos vinculados a um GM controlador, não Player Agents automáticos. O módulo independente `TabletopRpg.Npc` reage a eventos e interações explícitas, calcula propostas privadas de fala e as executa somente quando o host aprova e o `SessionEngine` autoriza. O estado cognitivo local é transitório, sem alteração do formato de campanha nem modelo conectado. Consulte `docs/npc-runtime-r1.md`.
