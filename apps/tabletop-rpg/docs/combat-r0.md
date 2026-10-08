# RPG-004 — Runtime de combate headless (R0)

**Situação:** candidato implementado em branch que depende do PR #310 (RPG-003).
Este arquivo registra apenas o comportamento implementado e testável no Core;
não constitui conclusão de R0 nem validação humana/device.

## Contratos

O combate é iniciado pela aplicação confiável com `CombatEncounter(campaign,
rules, orderedCombatants)`. Todos os IDs de personagem devem existir na campanha,
ter recurso `hp` com valor positivo e estar em uma sessão ativa. A ordem inicial
é passada explicitamente pelo Host e permanece estável enquanto o encontro existir.
Nenhum LLM recebe a instância mutável de `Campaign` ou `CombatEncounter`.

`SessionEngine` recebe opcionalmente esse encontro no construtor. Humans e
Agent Players usam o **mesmo** `GameIntent`:

- `AttackIntent(participant, character, target, attackKey)` — só durante seu
  turno, se participante controla a personagem, alvo é outro combatente vivo
  e a ação do turno não foi consumida.
- `EndCombatTurnIntent(participant, character)` — só por personagem controlada
  no próprio turno; avança para o próximo combatente vivo e incrementa rodada
  ao completar a volta. Não depende de timers, polling nem modelo.
- `ActionResolution.Combat` devolve hit/miss, valor da rolagem e **dano real
  aplicado**. Não divulga HP do alvo a um Player Agent.

`ICombatRules` é substituível. Implementações confiáveis resolvem o ataque
recebendo `Character`, `attackKey` e `IDiceRoller` do runtime, nunca dados
fabricados por texto de IA. `BasicD20CombatRules` é exemplo de referência,
não contrato de sistema obrigatório nem licença para importar regras D&D.
Rolagens/efeitos inválidos são rejeitados, não gastam ação, não alteram HP
nem escrevem evento.

Em acerto, dano é limitado ao HP remanescente do alvo e aplicado por
`Campaign.ChangeResource`, que gera seu evento de mutação. Cada ataque
válido e avanço de turno também emite evento ordenado; o último combatente
vivo encerra a disputa sem relógio/tarefa em segundo plano.
A autoridade do estado, inclusive HP e histórico, pertence à campanha.

## Persistência e limitações conscientes

O snapshot RPG-003 (schema v2) salva **HP e eventos de combate**. O
`CombatEncounter` é transitório: ordem, turno atual, ações gastas e rodada
não são restaurados automaticamente após reiniciar o aplicativo. Um novo
encontro precisa ser iniciado explicitamente no Host com os combatentes
vivos. Não reconstruir turno lendo strings do log como se fossem um segundo
formato canônico. RPG-032 trata da expansão futura para combate tático,
iniciativa/posicionamento e persistência da sessão de combate quando
autorizada uma revisão de schema. Nenhuma decisão de sistema de RPG é imposta
à campanha.

A API é pensada para execução serial do Host; sincronização concorrente e
rede são assuntos de RPG-050+. O estado secreto e as decisões do mestre
continuam fora de `PlayerView`. Não há UI, dados em mapa, IA real,
multiplayer, alteração de distribuição nem publicação de APK.
