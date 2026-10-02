# ADR-0015 — Integrador automático: estado combinado, fila serial, criticidade e autorização

## Status

Aceito. Os requisitos são do proprietário:

- **[ADD-0011](../governance/addenda/ADD-0011-colaboracao-multiagente-sem-verificacao-manual.md):** verificar o trabalho concorrente não pode depender de acionar um agente.
- **[ADD-0012](../governance/addenda/ADD-0012-automatico-por-padrao-humano-para-o-critico.md):** «Quero decidir e aprovar somente coisas críticas». A rotina integra sozinha; o crítico vai ao proprietário. ADD-0012 é o registro que encerra a DEC-0023.

O mecanismo abaixo é detalhe de implementação, decidido pelo agente dentro dessa direção (ADD-0008), como no ADR-0014. A política — o que é crítico — tem uma autoridade só: [`docs/governance/integration-policy.json`](../governance/integration-policy.json).

## Contexto

O ADR-0014 tornou verificável a integração de trabalho concorrente: `base_commit`, `--integration` (FRESH/STALE/sobreposição), checks no estado combinado e o PR como ponto de integração. Quem executava esses passos, porém, era um agente acionado pelo proprietário.

- FATO: o CI do PR roda sobre o merge do PR com a `main` **do momento do evento**. Se a `main` anda depois, o verde fica velho.
- FATO: `urbe-checks` e `lunet2d-ci` só rodam com filtro de caminho, em `pull_request` ou na `main`.
- FATO: pushes feitos com o `GITHUB_TOKEN` não disparam outros workflows; `workflow_dispatch` é a exceção.
- FATO: o repositório é de usuário, não de organização; a merge queue nativa do GitHub não está disponível.
- FATO: um merge de `apps/urbe` publica o Urbe Web, e um de `apps/lunet2d` gera release de desenvolvimento, depois da sincronização da origem.
- FATO (auditoria da primeira versão, 2026-10-02):
  - (1) o status registrava a `main` testada, mas não o commit combinado exato;
  - (2) a integração conferia só os pais do commit, não a identidade dele;
  - (3) o head do PR não era reconferido antes do push;
  - (4) a label `integrar` valia por existir, sem conferir quem a pôs;
  - (5) um PR podia mudar parte dos checks que o julgariam;
  - (6) o piso `owner-authorization` fazia o proprietário aprovar todo PR, o oposto do ADD-0012.

## Problema

Como levar à `main`, sem intervenção humana, o trabalho rotineiro testado **como ficará na `main`**, e levar ao proprietário só o que é crítico?

- Sem que um PR consiga classificar a si mesmo, enfraquecer os checks que o julgam ou se autoaprovar.
- Sem lock, e sem que o código do PR ganhe permissão de escrita.

## Opções

1. **Integrador próprio no GitHub Actions**, com fila serial, estado combinado, classificação por política da `main` e autorização conferida pelo ator.
2. **Merge queue do GitHub:** indisponível em repositório de usuário; não classifica criticidade.
3. **Auto-merge com branch protection:** exige alguém atualizando branches e não roda o CI de Product filtrado no estado combinado; não distingue rotina de crítico.
4. **Classificação por um modelo de IA** ("o agente acha seguro"): opaca, não auditável, rejeitada pelo proprietário.

## Decisão

Opção 1.

### Fila

O workflow é `.github/workflows/integrate.yml` e o componente é `integrator`.

- **Disparos:** cada evento de PR, cada push na `main`, `workflow_dispatch` e uma rede de segurança de hora em hora.
- **Uma execução por vez**, tratando **um** PR:
  - primeiro um PR crítico já testado contra a `main` atual e com a label de autorização (`land`);
  - senão, o de menor número que precisa de avaliação.
- Ficam fora: rascunhos, forks e Dependabot.

### Estado combinado, checker confiável e checks do candidato

1. O integrador faz merge do PR na `main` atual. O commit combinado registra no próprio histórico:
   - `Integration-Criticality`;
   - `Integration-Main`;
   - `Integration-PR-Head`;
   - `Integration-Handoffs`.
2. O resultado é publicado em `integration/pr-N`, branch descartável. Só ela aceita force.
3. Rodam dois conjuntos de verificação, um não substitui o outro:
   - **Checker confiável:** os checks da **`main`** (`tests/consistency/Check.cs` da cópia confiável) sobre a árvore candidata.
   - **Checks do candidato:** `consistency.yml` e o CI de cada Product tocado, por `workflow_call` com `ref` = commit combinado.
4. Conflito volta ao autor: a reconciliação é semântica (ADR-0014).

### Classificação de criticidade (rotina × crítico)

A política e as regras são sempre as da **`main`**:

- a política é lida por `--base-root .` da cópia confiável;
- as regras são funções C# do `Check.cs` da `main`.

Um PR nunca classifica a si mesmo. Um PR que muda a política, o integrador ou os checks é crítico pela política **anterior**.

**Zonas por caminho** (`paths`): qualquer mudança nelas é crítica. Elas falham fechado, mas só nas zonas conhecidas.

**Regras semânticas** (`rules`): só o que a regra detecta é crítico.

| Regra | Rotina | Crítico |
|-------|--------|---------|
| `decision-record` | perguntar (nova decisão pendente) | registrar ou alterar uma decisão tomada |
| `adr-decision` | propor um ADR | aceitar, rejeitar ou mudar a decisão de um ADR vinculante |
| `manifest-structure` | descrição, docs, status | criar ou remover componente; mudar tipo, caminho, dependências ou capabilities |
| `schema-breaking` | campo opcional novo | campo exigido, enum reduzido, `type` mudado |
| `capability-breaking` | versão nova | alterar ou remover uma versão publicada |
| `json-keys` | o resto do arquivo (ex.: dependências) | chaves de distribuição ou licença |
| `trust-weakening` | — | detalha checks removidos ou zonas desprotegidas |

**Classes:** constituição e autoridade; controle do próprio sistema; segurança; dados do usuário; distribuição crítica; compatibilidade; arquitetura transversal; estratégia de produto. As zonas e regras de cada uma estão na política.

**Agente:** pode **escalar** para crítico no handoff (`criticality.declared = critical`), nunca rebaixar.

**Falhas fechadas:**
- uma zona crítica que não pode ser analisada é crítica;
- sem política legível na `main`, tudo é crítico.

**O que não é crítico por si só:** tamanho do PR, feature nova, refatoração, testes, UI, documentação, ROADMAP, ARCHITECTURE, mudança no Urbe ou no Lunet2D.

### Integração

| Classificação | Condição | Efeito |
|---------------|----------|--------|
| **Rotina** | handoff válido, checker confiável verde, checks do candidato e CI dos Products verdes | integra **sozinha**, sem label |
| **Crítico** | tudo verde | status `pronto`, labels `critico` e `pronto-para-integrar`, comentário com a **consequência** de cada classe (da política) e o redisparo do portal ("Precisa de você"); entra só com a autorização do proprietário |

Na rotina, o checker confiável vermelho reprova. No crítico, a divergência vira informação para o proprietário, porque um PR que muda regras pode divergir das antigas legitimamente.

**Antes do único push para a `main`** (`land()`), o integrador confere:

1. O PR real ainda está aberto, fora de rascunho e com **head = head testado**. Se não, a avaliação fica `obsoleto` e o head novo é avaliado.
2. **Ref de integração = commit combinado testado.** Mesmos pais não bastam.
3. A **`main` atual = `main` testada**, e os pais do commit são a `main` e o head testados. Se não, reavalia.
4. Se exige o proprietário: o **evento** que pôs a label `integrar` foi feito por um autorizador da política (`@repository-owner` ou login explicitamente autorizado). Ele precisa ter ocorrido **depois** da avaliação deste head, contada pelo primeiro status `testando` do integrador. Label inválida é removida.

Depois disso, push **sem force** de exatamente esse commit. Em seguida o integrador dispara `pages`, `consistency` e a si mesmo.

### Identidade e visibilidade (NN-008, NN-021)

O status `ecosystem/integration` no head do PR tem o formato `<resultado> main=<sha> combined=<sha|-> <routine|critical|->[: detalhe]`. O `combined` é o commit **exato** testado.

Resultados possíveis: `testando`, `pronto`, `integrado`, `conflito`, `falhou`, `bloqueado`, `obsoleto`.

Cada PR tem um comentário do integrador com a rastreabilidade completa:
- `main`, head e combinado testados;
- classificação;
- handoff;
- checks e execução.

Visível não significa precisar de autorização.

### Decisão crítica × aprovação crítica

| | Decisão crítica | Aprovação crítica |
|---|---|---|
| O que é | escolha de direção ainda não tomada | a direção já está decidida, mas o efeito da execução é crítico |
| Caminho | DEC-XXXX no portal | PR crítico verde |
| O que o proprietário faz | escolhe | autoriza com a label |
| DEC nova? | sim | não |

Regras em [`communication.md`](../governance/communication.md) §9.

### Segurança (NN-016) e regressão

- `pull_request_target` executa a definição da `main`. O código do PR só roda nos checks, com token de leitura e sem segredos.
- Forks e Dependabot ficam fora. Texto do PR só entra em script por `env`.

**`CHK-INTEGRATION` prova estruturalmente:**
- a política existe e cobre o control plane e a constituição;
- toda regra da política tem implementação;
- não há `mergePolicy` paralelo (NN-001);
- a classificação usa a política da `main`;
- o checker confiável existe;
- o status persiste o commit combinado;
- `land()` confere head do PR, ref = commit testado, `main` e autor da label antes do único push;
- a rotina integra sozinha;
- não há force na `main` nem texto do PR interpolado;
- a simulação roda no CI.

**Cobertura de testes:**
- o self-test cobre fila, classificação e autorização;
- `simulate.sh` cobre, com git real e `gh` simulado com estado: as corridas (head e `main`), a ref mutada, a autorização (por não-proprietário e por proprietário) e a integração automática da rotina.

## Consequências

- **Agentes:**
  - fazem o trabalho em branch própria;
  - registram o handoff em `review` (ou `done`, se só faltar integrar);
  - deixam o PR pronto;
  - corrigem quando o integrador devolve.

  Ninguém integra à mão. **Nenhum agente põe a label `integrar`**. Agentes não simulam eventos do proprietário.
- **Proprietário:** recebe só decisões críticas, aprovações críticas e validações críticas, no portal e no PR, com a consequência explicada. Não revisa PR rotineiro.
- **Urbe e Lunet2D:** rotina integra sozinha. As zonas críticas de cada um ficam na política:
  - dados;
  - fronteira de confiança do desktop e plugins;
  - credenciais;
  - assinatura e identidade do app;
  - canal de publicação;
  - licença;
  - decisões consolidadas (ADRs).
- **Limite de identidade da plataforma:** se um agente operar com a credencial do próprio proprietário, o GitHub não distingue "o proprietário clicou" de "o agente usou a credencial dele". A proteção é de processo:
  - agentes nunca põem labels críticas;
  - agentes nunca simulam eventos do proprietário;
  - tokens de automação têm privilégio mínimo.

  Não há solução técnica dentro do GitHub para isso.
- **Limites conhecidos:**
  - O integrador não é branch protection: quem tem escrita ainda pode empurrar na `main`, e agentes não devem.
  - Zonas de dados cobrem os módulos de persistência declarados. Formato persistido em arquivos comuns (ex.: `app.js` do Urbe) depende da escalada do agente e dos testes de formato do produto. A regra do Urbe exige atualizar `DATA-CATALOG.md`, que é zona crítica.
  - A sincronização das origens depende do agendamento delas (DEC-0017-A).
- **Custo:**
  - o checker confiável roda uma vez por avaliação;
  - a fila é serial;
  - a simulação adiciona cerca de 30 s ao CI de consistência.

## Alternativas rejeitadas

- **Integrar o head do PR com o verde do evento:** pode levar à `main` um estado nunca testado.
- **Conferir só os pais do commit combinado:** aceita outro commit com os mesmos pais.
- **Confiar na presença da label:** qualquer um com acesso de triagem poderia autorizar.
- **Piso `owner-authorization` para tudo:** contraria o ADD-0012 (proprietário como CI humano).
- **Classificar por tamanho ou tipo de arquivo, sem semântica:** escala o que não é crítico; o ADD-0012 pede o contrário.
- **Resolver conflitos automaticamente; lock:** rejeitados no ADR-0014 e no ADD-0010.

## Referências

MANIFEST §22.4, §23, §24, §27, §30; NN-001, NN-005, NN-008, NN-009, NN-011, NN-016, NN-017, NN-018, NN-021, NN-022; ADD-0008, ADD-0010, ADD-0011, ADD-0012; ADR-0014; DEC-0017, DEC-0023; [`integration-policy.json`](../governance/integration-policy.json); [`multi-agent.md`](../governance/multi-agent.md) §5; [`communication.md`](../governance/communication.md) §9.
