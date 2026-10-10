# Persistência de campanha v2 (RPG-003)

> Compatível com `docs/persistence-v1.md`. O ID de formato permanece
> `tabletop-rpg-campaign`; a versão de schema é **2** (independente da versão do aplicativo).
> Esta alteração de dados do usuário é **critical / user-data** e depende de aprovação
> de integração conforme a política canônica do Ecosystem.

## Estado adicional

O payload v2 conserva todos os campos v1 sem mudar o significado de seus IDs, sessões,
eventos, conhecimento ou memórias e exige `world` com:

- `fictionMinutes`: minutos inteiros não negativos decorridos **dentro da ficção**.
  Nunca se confunde com `clock`, que ordena eventos e sessões e não é horário físico.
- `activeSceneId`: ID de uma cena cadastrada, ou `null`.
- `scenes`: `id`, `title`, `description`; não há publicação implícita ao jogador.
- `quests`: `id`, `title`, `status` (open/completed/failed),
  `sceneId` opcional (deve apontar para cena existente).
- `inventory`: IDs de entradas, `owner` apontando para CharacterId cadastrado,
  `itemKey` (slug), `quantity` positiva.
- `conditions`: chave por `characterId` + `key` e `expiresAtMinute`
  opcional; condições expiram quando o relógio ficcional as alcança.
- `resources`: chave por `characterId` + `key`, `current` e `maximum`
  com `0 ≤ current ≤ maximum ≤ 1 000 000`.

Uma mudança de cena, missão, inventário, condição ou recurso registra um
`GameEvent` ordenado pelo relógio lógico, sem invocar qualquer modelo de IA
nem motor de regras específico. O Core não escolhe sistema comercial.
O novo estado não é inserido automaticamente em `PlayerView`: a futura
camada de intenções/visão deve filtrar informações secretas pela autoridade
da sessão; **o Core não permite inferir que acesso ao objeto `Campaign` equivale
a acesso de um agente**.

## Compatibilidade

- Escrita: sempre v2, envelope JSON com SHA-256 canônico como na v1.
- Leitura: v2 validado integralmente ou v1 reconhecido e convertido **em memória**
  para `world` vazio e `fictionMinutes=0`, preservando todas as estruturas
  e os IDs anteriores. Não altera arquivo original ao importar.
- Leitor antigo v1 não pode abrir v2; uso simultâneo entre clientes antigos
  **não** é suportado. Antes de salvar, usuários devem preservar backup/export
  v1 de campanhas anteriores. `FileCampaignStore` mantém salvamento com backup,
  integridade e recuperação definidos em RPG-002.
- Versões futuras e schema v1 contendo campos v2 são recusados.
- Qualquer referência dangling, ID vazio/duplicado, item de dono desconhecido,
  número inválido ou catálogo com campos estruturais ausentes é recusado;
  não há reparo silencioso de mundo importado.
- O checksum detecta corrupção acidental, não autenticação criptográfica.

## Estado do Product

Slice **headless** em RPG-003: nenhuma UI Android, multiplayer, provedor IA,
motor de combate, crafting, quest-giver automático nem propagação implícita
de conhecimento. Validação de gameplay visual/Android ainda não foi executada.
