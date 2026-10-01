# ADR-0015 — Integrador automático: estado combinado, fila serial e política de integração

## Status

Aceito. O requisito é do proprietário ([ADD-0011](../governance/addenda/ADD-0011-colaboracao-multiagente-sem-verificacao-manual.md), inclusive a correção «Automaticamente*»): verificar o trabalho concorrente não pode depender de acionar um agente. O mecanismo é detalhe de implementação, decidido pelo agente sob a delegação de [ADD-0008](../governance/addenda/ADD-0008-delegacao-ate-a-refatoracao.md), como no ADR-0014. A única escolha de direção — se o integrador leva trabalho à `main` **sem nenhum toque** do proprietário — é **DEC-0023, pendente**. Até a decisão, o piso da política é `owner-authorization`: o integrador verifica tudo sozinho e só integra o que o proprietário autorizar.

## Contexto

O ADR-0014 tornou a integração concorrente verificável: `base_commit`, `--integration` (FRESH/STALE/sobreposição), checks no estado combinado e PR como ponto de integração. Mas quem executava esses passos era um agente integrador acionado pelo proprietário. O ADD-0011 chama isso de insustentável.

- FATO: o CI do PR roda sobre o merge do PR com a `main` **do momento do evento**. Se a `main` anda depois, o verde fica velho e ninguém retesta.
- FATO: `urbe-checks` e `lunet2d-ci` só rodam com filtro de caminho, em `pull_request` ou na `main`.
- FATO: pushes feitos com o `GITHUB_TOKEN` não disparam outros workflows; `workflow_dispatch` é a exceção.
- FATO: o repositório pertence a um usuário, não a uma organização. A merge queue nativa do GitHub não está disponível.
- FATO: um merge na `main` de `apps/urbe` publica o Urbe Web, e um de `apps/lunet2d` gera uma release de desenvolvimento, nos dois casos depois da sincronização da origem.

## Problema

Como levar à `main`, sem verificação manual, só estados que foram testados **como ficarão na `main`**? É preciso fazer isso:

- respeitando as regras de merge de cada Product;
- sem lock;
- sem que o código de um PR ganhe permissão de escrita;
- deixando com o proprietário a escolha de dispensar ou não o seu toque.

## Opções

1. **Integrador próprio no GitHub Actions.** Uma fila serial monta o estado combinado `main atual + PR`, roda nele os checks do Ecosystem e o CI dos Products tocados e então avança a `main` exatamente para esse commit, sem force.
2. **Merge queue do GitHub.** Indisponível em repositório de usuário.
3. **Auto-merge do GitHub com branch protection** ("require branches to be up to date"). Exige que alguém atualize a branch a cada mudança da `main`, o que traz de volta o agente. Também não roda o CI de Product filtrado por caminho no estado combinado.
4. **Manter o agente integrador sob demanda** (o modelo do ADR-0014). Rejeitado pelo ADD-0011.

## Decisão

Opção 1. Ela tem cinco partes.

### Fila

Arquivo: `.github/workflows/integrate.yml`; componente `integrator` em `ecosystem.json`.

**Disparos:** cada evento de PR, cada push na `main`, `workflow_dispatch` e, de hora em hora, uma rede de segurança.

**Uma execução por vez** (`concurrency: integrate`), e cada execução trata **um** PR:

- primeiro um PR já testado contra a `main` atual e autorizado (`land`);
- senão, o de menor número que precisa de avaliação (`evaluate`): nunca avaliado, avaliado contra outra `main`, ou com avaliação interrompida.

Ficam fora da fila: rascunhos, PRs de fork e PRs do Dependabot. Enquanto houver PR a tratar, a execução se redispara.

### Estado combinado e checks

A execução faz merge do PR na `main` atual e publica o resultado em `integration/pr-N`. Essa branch é descartável, do integrador; só ela aceita push forçado.

No commit combinado rodam:

- `consistency.yml`;
- o CI de cada Product tocado (`urbe-checks.yml`, `lunet2d-ci.yml`).

Esses workflows passam a aceitar `workflow_call` com a entrada `ref`.

Um conflito não é resolvido pelo integrador: a reconciliação é semântica e cabe ao autor (ADR-0014).

### Portões

As decisões são funções C# puras em `tests/consistency/Check.cs` (`--integration-plan`, `--integration-gates`), provadas pelo self-test. O shell é só cola de git/gh; é exceção local ao padrão C# de NN-005, como em `origin-sync`. O efeito dessa cola no git é provado no CI por `.github/integrator/simulate.sh`, que usa um `origin` local e o `gh` simulado.

- **Handoff (NN-008):** o PR precisa trazer pelo menos um handoff em `review`, `verifying` ou `done`.
- **Política, sempre lida da `main`:** vale a mais restritiva entre o piso do Ecosystem (`ecosystem.mergePolicy`) e o `mergePolicy` dos componentes tocados.
  - `automatic` integra sozinho.
  - `owner-authorization` exige a label `integrar`, posta pelo proprietário.
  - O Urbe é `owner-authorization`: a label é o seu "pedido explícito".
- **Política não se autoaprova:** um PR que muda qualquer `mergePolicy` exige autorização.

### Integração

O integrador avança a `main` para o commit combinado testado com push **sem force**, e só se a `main` ainda for o primeiro pai desse commit. Se a `main` mudou, ele reavalia.

Depois de integrar, dispara `pages`, `consistency` e a si mesmo, porque o push feito pelo bot não dispara workflows.

O GitHub marca o PR como mergeado, pois o head do PR passa a estar na `main`.

### Visibilidade (NN-008, NN-021)

**Status `ecosystem/integration` no head do PR.** O formato da descrição é `<resultado> main=<sha12>: …`, com estes resultados:

- `testando`
- `pronto`
- `integrado`
- `conflito`
- `falhou`
- `bloqueado`

**Um comentário do integrador por PR**, atualizado a cada avaliação.

**Labels:**

- `pronto-para-integrar`: verde, aguardando autorização;
- `precisa-reconciliar`: conflito ou check vermelho, ação do autor.

### Segurança (NN-016)

- `pull_request_target` executa a definição de workflow que está na `main`.
- O código do PR só roda nos jobs de checks, com token de leitura e sem segredos.
- PRs de fork são ignorados.
- Texto do PR entra em script só por variável de ambiente.

`CHK-INTEGRATION` fiscaliza estas propriedades:

- o gatilho `pull_request_target`;
- a lógica testada;
- o CI de cada Product no estado combinado;
- a ausência de push forçado fora de `integration/*`;
- a simulação do integrador no CI;
- a ausência de texto do PR interpolado;
- o piso e o `mergePolicy` de cada Product ativo.

## Consequências

- **Agentes:** a branch fica como PR rascunho, ou sem PR, enquanto o agente trabalha. Ao terminar, o agente:
  - grava o handoff em `review`;
  - marca o PR como pronto para revisão;
  - corrige e envia de novo quando o integrador devolve (conflito ou check vermelho).

  Ninguém integra à mão. **Nenhum agente adiciona a label `integrar`**: ela é a autorização do proprietário (MANIFEST §23.2).
- **O handoff pode ir como `done` no PR** quando só falta a integração. O integrador só integra o que passou, então na `main` `done` continua significando "integrado e verificado" (ADR-0014). Validação humana pendente continua `review` (NN-017).
- **Proprietário:** com o piso `owner-authorization`, o toque é a label `integrar` num PR marcado `pronto-para-integrar`. O integrador leva à `main` exatamente o estado testado e retesta se a `main` tiver mudado. Se DEC-0023 for decidida A, um agente muda o piso para `automatic`; esse PR ainda pede a label uma vez, e a partir dele só o Urbe pede.
- **Custo:** cada integração roda os checks no estado combinado, de novo a cada mudança da `main`; a fila é serial. Com poucos PRs simultâneos, o custo é aceitável.
- **Limites conhecidos:**
  - O integrador não é branch protection: quem tem escrita ainda pode empurrar na `main`, e agentes não devem.
  - O teste do estado combinado usa os checks do próprio PR, como qualquer CI.
  - A sincronização da origem dos Products depende do agendamento dela, porque não há token entre repositórios (DEC-0017-A). O portal mostra quando um espelho está atrasado.

## Alternativas rejeitadas

- **Integrar o head do PR com o verde do evento:** pode levar à `main` um estado nunca testado.
- **Resolver conflitos automaticamente** (`--ours`/`--theirs`): proibido em arquivo normativo (ADR-0014).
- **Lock por arquivo ou agente:** o ADD-0010 rejeita lock.

## Referências

MANIFEST §23, §24, §27; NN-001, NN-005, NN-008, NN-011, NN-014, NN-016, NN-017, NN-018, NN-021; ADD-0008, ADD-0010, ADD-0011; ADR-0014; DEC-0017, DEC-0023; [`multi-agent.md`](../governance/multi-agent.md) §5.
