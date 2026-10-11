# RPG-016 — Campanha imediata sem preparação (R1)

**Objetivo do roadmap:** um humano inicia uma campanha solo jogável em um clique
(ou apenas um comando de criação), sem escrever cenário, Mestre, personagens
ou configurações de provedor antes de a sessão começar.

## API headless

`QuickStartFactory.Create()` inicializa um jogo com opções padrão.
`QuickStartFactory.Create(new QuickStartOptions(theme, heroName, campaignName))`
aceita apenas as três escolhas opcionais abaixo. A lista pública
`QuickStartFactory.Choices` mostra títulos e pitches **sem fatos secretos**:

| Tema | Enredo inicial |
| --- | --- |
| Fantasy | desaparecidos e luz azul sob uma ponte |
| Mystery | atriz desaparecida na noite de estreia |
| Frontier | sinal interrompido de um posto remoto |

O resultado `QuickStartResult` contém:

- `Campaign` canônica com três personagens e três assentos:
  humano jogador (herói), jogador agente (companheiro) e Mestre agente
  (controlando explicitamente um NPC); nenhum NPC vira Player Agent por padrão.
- Cena inicial ativa, quest em aberto ligada à cena, HP básico dos
  personagens, um item inicial e **sessão realmente iniciada**. Um humano
  pode agir de imediato via `SessionEngine` existente.
- Um gancho privado de mundo, conhecido pela `GameMasterView`,
  **não revelado** na visão humana ou do companheiro. O jogador recebe
  uma pista como crença de confiança moderada, não como verdade canônica.
- `PlayerAgentProfile` do companheiro, `NpcProfile` e três
  `DirectorThread` (ameaça/pista/promessa). Esses perfis são
  transient resources de bootstrap: cada runtime R1 recebe o seu objeto
  somente quando o Host habilita uma sessão de IA.
- `OpeningText` público para exibir na tela. Não contém o segredo,
  não é inserido automaticamente no histórico como fala do Mestre
  e não habilita tools/permissões.

## Integração e operações

```csharp
using TabletopRpg.QuickStart;

var start = QuickStartFactory.Create();
var campaign = start.Campaign;

// O Host pode persistir imediatamente com CampaignCodec.Encode(campaign)
// ou iniciar ações via SessionEngine usando os IDs de start.
// GameMaster/Player Agent só passam a chamar modelo após configuração
// explícita do Host e gatilhos de sessão.
```

O `CampaignCodec` v2 atual já serializa os dados da campanha (IDs,
participantes, cena, missões, eventos, fatos, conhecimento, memória, sessão,
recursos e inventário). O código de criação não altera schema.

Os perfis transitórios de NPC/companheiro/Director e o texto de
abertura **não** são gravados no schema v2. Eles devem ser reconstituídos
pelo Host ao retomar a sessão; reexecutar `QuickStartFactory.Create()`
cria **outra** campanha e não deve ser usado para reconstruir dados
existentes. Portanto, a retomada de personalidade/RPG-015 ainda não está
inteiramente concluída.

## Fronteira de IA e limitações

- A criação é síncrona, local, sem rede e sem custo de tokens,
  compatível com qualquer modelo/provider via os contratos de
  Gateway do Product. Não fixa nenhuma marca, endpoint ou modelo.
- Não cria roteiro obrigatório: só define situação inicial, pistas,
  ameaças e promessa. O Mestre e o Director evoluem o mundo por eventos.
- Não dispara o Mestre IA automaticamente. A construção do turno de
  modelo, o aval de narrativas do GM, a política de provedores do Host
  e a UI (fase R2) são responsabilidades separadas.
- Não implementa multiplayer, servidor, editor de fichas, mobile
  ou biblioteca proprietária de regras.
- Criar quickstart **não** implica gate G-R1 fechado: exige uma sessão
  solo completa com modelos configurados, regras e observabilidade sem
  vazamentos ou custos ociosos.

## Verificação

`dotnet test --project apps/tabletop-rpg/tests/TabletopRpg.Core.Tests`

Testes: três temas, criação sem opções, campanha pronta e com assentos
corretos, limites de autorização, PlayerView sem segredos, Engine aceita
ação humana/companheiro, perfis carregam em NPC/Director, identidade
única e roundtrip pelo codec v2 para todos os temas.

**Dependência de integração:** RPG-016 está empilhada sobre RPG-015
(PR #523), que depende dos PRs #521 e #519. Não promover até
integração governada e testes combined.
