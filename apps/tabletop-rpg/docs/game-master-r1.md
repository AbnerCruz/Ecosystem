# RPG-012 — Game Master Agent (R1)

## Papel e autoridade

O Mestre IA ocupa um assento `SeatRole.GameMaster` com `ControllerKind.Agent`.
Ele não é um Player Agent, nem utiliza a perspectiva filtrada de uma personagem.

`Campaign.CreateGameMasterView(participantId)` produz uma **projeção separada**
para assentos de Mestre: identidade, revisão da campanha, relógio ficcional, cena
ativa, fatos verdadeiros do mundo, cenas, missões, nomes/IDs das personagens e
eventos recentes. As coleções retornadas são snapshots de leitura; nenhum
ponteiro para `Campaign` chega ao modelo. `CreatePlayerView` continua sem
acesso automático aos segredos ou aos conhecimentos de outras personagens.

## Execução e ferramentas

`GameMasterAgent.ProposeAsync(gmId, wake)` só chama `IModelGateway` após um
gatilho permitido: `session-started`, `player-action`, `scene-changed`,
`consequence` ou `addressed`. Polling e ociosidade são recusados.

O `GameMasterProposalDecoder` valida um objeto JSON único, sem campos
desconhecidos, chaves duplicadas ou identificadores arbitrários do Mestre:

- `{"kind":"narrate","text":"..."}`: gera um `NarrationDraft` PRIVADO.
- `{"kind":"reveal_fact","factId":"GUID-N","targetCharacterId":"GUID-N"}`: gera um `RevealFactDraft` PRIVADO.
- `{"kind":"pass"}`: nenhuma operação.

**Nenhuma proposta é executada ao receber o texto do modelo.**
Somente código confiável do host pode chamar
`ApprovedGameMasterTools.ApplyApproved(proposal)`. Essa camada delega às
operações canônicas de `Campaign`, que verificam o assento de Mestre,
a revisão composta (relógio lógico e contagens de fatos, personagens e participantes), a existência dos fatos e o destino antes de
registrar um evento ou revelar conhecimento à personagem de destino.
Falas propostas, inclusive segredos, **não** entram no histórico público
até aprovação explícita. Mudanças na campanha após a proposta tornam sua
revisão obsoleta e são recusadas sem evento extra.

**Importante:** a API separa aprovação de proposta, mas ainda não existe
host de UI para revisão humana, nem política automatizada para quais
propostas devem ser aprovadas. Não tratar o `GameMasterAgent` como mestre
autônomo/jogável em produção.

## Limites

- Não pode jogar dados, forçar combate, alterar HP, quests, inventário,
  objetos ou outras propriedades canônicas por texto do modelo.
- Não há novo schema de campanha, persistência de perfil de Mestre,
  servidor, multiplayer, Android, UI ou adaptador HTTP de provedor.
- Personagem Player Agent continua isolada: não recebe `GameMasterView`.
- O contexto do Mestre usa limites de tamanho e recorte das listas:
  não promete resumir todo o mundo nem representar memória de longo prazo;
  orquestração, budgets e recuperação robusta continuam em RPG-015.
- As propostas são transitórias; após reinício, o host deve reconstituir
  o estado a partir do save e gerar novas propostas.
- O relógio lógico é usado como token de revisão, não como tempo ficcional.
- A narração é texto não confiável; host deve revisar vazamentos antes de
  aprovar qualquer publicação aos jogadores.

## Testes

```sh
cd apps/tabletop-rpg
dotnet test --project tests/TabletopRpg.Core.Tests
```

Testam isolamento GM/Player, visão privada, wake explícito, cancelamento,
falhas de JSON, rejeição por identidade, mundo inválido, revisão obsoleta,
narrativa somente após aprovação e revelação para personagem determinada.

**Dependências de integração:** RPG-010 PR #506 (integrado) e RPG-011
PR #511 (ainda dependente). O RPG-012 fica `[~]` até CI, revisão e
integração do PR correspondente. Nenhum DEVICE é reivindicado.
