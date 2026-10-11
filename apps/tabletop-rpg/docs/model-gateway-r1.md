# RPG-010 — Gateway local de modelos (R1)

## Fronteira implementada

`TabletopRpg.ModelGateway` é uma assembly C# própria do Product, sem
`PackageReference`, rede, SDK de provedor, UI, Hub ou dependência de outro
Product. `IModelGateway.GenerateAsync` recebe um único `ModelTurnRequest`
sob chamada explícita e devolve `ModelTurnResponse` **não confiável**.

- `ModelTurnRequest` transporta apenas mensagens textuais que o chamador
  projetou explicitamente para o participante. Não aceita `Campaign`,
  `WorldState` nem referência de entidade mutável.
- Valida turn ID/contrato, papéis, quantidade e limites de texto. Copia as
  mensagens em snapshot somente leitura; não envia contexto implicitamente.
- `ResponseContract` identifica a forma de resposta solicitada (por exemplo,
  `game-intent.v1`), **não** autoriza executá-la. RPG-011 deverá interpretar
  a resposta como intent e passar por `SessionEngine.Execute` para validar
  controlador, personagem e regras.
- O contrato não faz polling, não liga agentes automaticamente e não inicia
  chamadas de rede. RPG-011/015 cuidam dos disparos, orçamentos e guardas.
- O output de um modelo não altera campanha ou rolagem diretamente.

## Configuração não secreta

`ModelGatewaySettings.FromEnvironment()` lê:

| Variável | Conteúdo |
|---|---|
| `TABLETOP_RPG_MODEL_PROVIDER` | ID local do adaptador/provedor |
| `TABLETOP_RPG_MODEL_ID` | Nome do modelo |
| `TABLETOP_RPG_MODEL_ENDPOINT` | URL HTTPS ou HTTP loopback |
| `TABLETOP_RPG_MODEL_CREDENTIAL_ENV` | **Nome** de outra variável de ambiente que conterá o segredo (opcional) |

Exemplo local, **sem chave**:

```sh
export TABLETOP_RPG_MODEL_PROVIDER=local
export TABLETOP_RPG_MODEL_ID=my-model
export TABLETOP_RPG_MODEL_ENDPOINT=http://127.0.0.1:11434/api
```

Exemplo remoto: configurar endpoint HTTPS e o *nome* da variável de
credencial; definir o valor secreto apenas no ambiente do processo. A
`EnvironmentModelCredentialSource` só lê essa variável no momento em que
o futuro adaptador solicitar a credencial, sem gravar no arquivo do jogo.

Regras: sem userinfo, query ou fragment em endpoint; HTTP remoto rejeitado;
sem chave em URL, configuração de campanha, inventário, evento, histórico,
log ou export. A configuração **não** é incorporada ao
`TabletopRpg.Persistence`, nem altera o schema v2.

## Limites explícitos

Esta fase estabelece contrato e configuração; **não** implementa HTTP client,
adaptador de um provedor real, gerenciamento de chave do Android, Mestre IA,
Player Agent, streaming, parser/validador de intents de modelo ou UI.
Isso pertence aos próximos itens R1/R2. `EnvironmentModelCredentialSource`
é uma fonte para hosts que suportam variáveis de ambiente; RPG-020/025
deverão selecionar armazenamento seguro adequado ao aparelho sem levar
segredos à campanha.

## Verificação

```sh
cd apps/tabletop-rpg
dotnet test --project tests/TabletopRpg.Core.Tests
```

Cobertura: snapshot imutável, limites de request, endpoint seguro, ausência
de leitura antecipada de segredo, resolução em tempo de uso e falta de
execução ociosa. Nenhuma afirmação de DEVICE ou de integração com provedor.
