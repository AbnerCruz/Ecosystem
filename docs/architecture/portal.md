# Portal web do Ecosystem (GitHub Pages)

> **Autoridade:** documento de arquitetura, subordinado ao `MANIFEST.md`, ao adendo do proprietário [ADD-0001](../governance/addenda/ADD-0001-portal-github-pages.md) e ao [ADR-0005](../adr/0005-portal-web-github-pages.md).
> Componente `portal` em [`ecosystem.json`](../../ecosystem.json). URL pública: https://abnercruz.github.io/Ecosystem/

## 1. Três superfícies, três papéis

| Superfície | Papel | Pergunta que responde |
|------------|-------|-----------------------|
| **GitHub** | fonte técnica: código, histórico, CI, releases, ADRs | "Onde está a verdade?" |
| **Portal (GitHub Pages)** | portal humano de desenvolvimento, distribuição, documentação, testes e **recuperação** | "Estou com o celular e quero saber o estado do Ecosystem, baixar/testar um app ou recuperar o próprio Hub. Para onde vou?" |
| **Ecosystem Hub** (C#, Fase 3+) | control plane completo | "Quero controlar profundamente o ecossistema, workspaces, capabilities, agentes, instalações, atualizações e integrações." |

O portal **não** é o Hub, não o substitui, não implementa o Hub em HTML e não duplica funções que pertencem ao Hub (Control Plane, Past/Now/Next, workspaces, capabilities). **Exceção decidida pelo proprietário (DEC-0007):** o portal apresenta, como projeção somente leitura, as decisões pendentes e as validações humanas pendentes, sempre com o objeto a decidir/validar (§3.1); o Hub mantém a superfície completa de decisões (MANIFEST §35). Ele continua existindo depois que o Hub existir, como mecanismo de recuperação independente: com o Hub ausente ou quebrado, deve continuar sendo possível acessar a documentação, baixar Hub e Lunet2D, acessar o Urbe Web, localizar releases e localizar instruções de recuperação e teste.

### Publicação após releases automáticas (PORTAL-20261006.1)

O portal projeta as versões e artefatos do GitHub **durante seu build**; não consulta
o GitHub para preencher versões estáticas no dispositivo. O gatilho
`release.published` continua cobrindo releases publicadas manualmente ou por
credenciais capazes de emitir eventos de workflow. Porém, os pipelines internos
`hub-release`, `lunet2d-release` e `urbe-release` publicam releases com
`GITHUB_TOKEN`: esses eventos **não** acionam outros workflows GitHub Actions.

Por isso `.github/workflows/pages.yml` também usa `workflow_run: completed`
nesses três produtores. Só constrói após conclusão `success`, do mesmo
repositório, e nunca após `pull_request` (o Lunet testa PRs sem publicar).
Em todos os eventos, `checkout` usa `github.event.repository.default_branch`,
não a tag nem artefatos ou código do produtor. A fila `concurrency: pages`
preserva a ordem de publicações e a projeção sempre consulta as releases
existentes naquele instante; eventos repetidos são seguros. Não se cria PAT,
ação manual nem dependência adicional dos produtos no Portal.

`CHK-PORTAL` e os seus self-tests verificam gatilhos, filtro de sucesso/origem,
e a fonte canônica do checkout. O smoke test de Pages continua necessário:
CI verde não comprova que o conteúdo publicado está servível na URL pública.
## 2. Nunca fonte de verdade, nunca dependência

```text
MANIFEST / ecosystem.json / ROADMAP / releases / CI / registros de validação
                              ↓
          site/generator/GenerateStatus.cs  (no CI)
                              ↓
          site/data/ecosystem-status.json   (projeção, authority: false)
                              ↓
          site/index.html + app.js          (apenas renderiza)
                              ↓
                        GitHub Pages
```

- Nenhum dado canônico é escrito à mão em `site/` (ex.: proibido `const lunetVersion = "0.4.2"`). `CHK-PORTAL` recusa versões, IDs de decisão, handoff, invariante e fase escritos literalmente nos arquivos publicados.
- A projeção não é versionada (`.gitignore`): é gerada a cada publicação. Se existir localmente, `CHK-PORTAL` a valida contra o schema e contra as fontes canônicas (componentes, fase, repositório, validações pendentes, documentos).
- Todo dado projetado declara proveniência: `derived` (com a fonte citada) ou `not-available` (com o motivo). O portal mostra "não disponível" em vez de inventar.
- Nenhum componente pode declarar dependência do portal (`CHK-BOUNDARIES`). Urbe, Lunet2D e Hub nunca o consultam em runtime.

## 3. Contrato da projeção

Autoridade do formato: [`docs/contracts/schemas/ecosystem-status.schema.json`](../contracts/schemas/ecosystem-status.schema.json) (`ecosystem/contracts/ecosystem-status/1`). Resumo:

| Campo | Conteúdo |
|-------|----------|
| `kind`, `authority` | sempre `projection` / `false` |
| `source` | repositório, ref, commit e lista de arquivos canônicos lidos |
| `ecosystem` | nome, fase e resultado dos checks de consistência do commit projetado |
| `ecosystem.gates[]` | estado do gate de cada fase, derivado do ROADMAP (`*Estado do gate:*`), com o nome da fase e a contagem dos itens por caixa; `CHK-PORTAL` recusa divergência |
| `components[]` | ID, nome, tipo, status, descrição, links (repositório, releases, web) e os dados `version`, `release`, `ci` com proveniência; `artifacts[]` (APK, instalador Windows, AppImage da última release, com tamanho e SHA-256; P1-9); `validation.state` |
| `pendingValidations[]` | validações humanas pendentes derivadas dos registros canônicos (hoje, handoffs) |
| `docs[]` | documentação canônica com link |

O formato de exemplo do adendo (`products.lunet2d.version` etc.) foi analisado e substituído por este contrato porque (a) cada dado precisa carregar proveniência para que "não disponível" nunca vire um valor inventado; (b) todos os componentes do `ecosystem.json` são projetados, não só produtos, para que a verificação de divergência seja total; (c) validação humana tem estados próprios, separados de CI.

### 3.1 Decisões e validações pendentes, com objeto (DEC-0007)

- **Decisão a tomar:** `decisions.json` com `status: pending` e `related` apontando para o(s) documento(s)/artefato(s) a revisar. O portal mostra título, pergunta, alternativas com consequências, recomendação do agente e o objeto como botões.
- **Validação humana pendente:** verificação `kind: human`, `result: pending` em um handoff, com `object` (caminho do repositório ou URL).
- Sem objeto, a pendência não é registrável: `CHK-DECISIONS` e `CHK-HANDOFFS` falham, o gerador aborta e `CHK-PORTAL` recusa uma projeção que omita qualquer pendência.
- O portal não escreve nada sozinho. A resposta a uma decisão segue o fluxo de §3.2.

### 3.2 Responder a uma decisão pelo portal (DEC-0010, ADR-0007)

```text
portal: "Escolher B"  ──►  GitHub abre uma Issue já preenchida  ──►  proprietário toca "Submit new issue"
   ──►  workflow decision.yml (só o dono do repositório)  ──►  aplicador valida e grava
   ──►  checks de consistência  ──►  commit na branch padrão  ──►  portal republicado  ──►  Issue comentada e fechada
```

- **Dois toques:** a escolha no portal e a confirmação no GitHub. A confirmação é intencional: o site estático não tem credencial de escrita (nenhum token no navegador).
- **Formato único:** o gerador da projeção define o título e o corpo da Issue; o aplicador os valida (marcador `ecosystem-decision:v1`, decisão, letra e hash do texto da alternativa). Uma escolha cujo texto mudou depois de exibida é recusada.
- **Registro:** `docs/governance/decisions.json` (decisão `decided`, data, texto, `record`) e `docs/governance/responses/DEC-NNNN.md`.
- **Depois do registro:** um agente aplica as consequências nos documentos dependentes e registra o handoff; decisão estrutural continua exigindo ADR (NN-011).
- **Quem pode responder:** só o proprietário. Ver [`access.md`](access.md) (quem vê, quem decide e por que uma senha no portal não protege).
- **Agentes nunca respondem uma decisão pendente do proprietário**, nem criando essa Issue (MANIFEST §23.2).

### 3.3 Aprovar ou reprovar uma validação pelo portal (ADD-0005, ADR-0008)

Cada validação humana pendente tem **Aprovar** e **Reprovar**. É o mesmo fluxo de §3.2 (Issue pré-preenchida, confirmada no GitHub, só o dono), com o marcador `ecosystem-validation:v1` e o título `Validação <tarefa>: aprovada|reprovada`.

- **Registro:** o resultado (`passed`/`failed`) e a evidência vão para a **própria verificação** do handoff (fonte única, NN-001) e para `docs/governance/responses/VAL-<handoff>-<hash>.md`. O `state` do handoff e o ROADMAP continuam sendo atualizados por um agente.
- **Recusas:** handoff que não é o mais recente da tarefa ou já encerrado, verificação que já foi respondida ou cujo texto mudou (hash), título e corpo discordantes, autor que não é o dono.
- **Reprovação:** o motivo opcional, escrito depois de `comment:`, entra no registro como citação (dado, nunca instrução).
- **Agentes nunca respondem uma validação pendente do proprietário.**

Evolução incompatível incrementa `schemaVersion` e exige ADR.

## 4. Estados de validação

O portal mostra CI e validação humana em linhas **separadas**. Estados de validação de build: `UNKNOWN` (sem registro), `IMPLEMENTED`, `AUTOMATED_VERIFIED`, `HUMAN_VALIDATION_PENDING`, `VALIDATED` — definidos em [`definition-of-done.md` §4](../governance/definition-of-done.md). CI verde leva no máximo a `AUTOMATED_VERIFIED`; `VALIDATED` exige evidência de validação humana (`CHK-PORTAL`, NN-017).

## 5. Publicação

Workflow próprio [`.github/workflows/pages.yml`](../../.github/workflows/pages.yml), separado do workflow de consistência:

```text
push na branch padrão, release publicada (ou execução manual)
↓ checkout da branch padrão canônica
↓ checks de consistência        (falha → não publica; o site anterior continua no ar)
↓ gera a projeção e consulta releases publicadas
↓ valida a projeção (CHK-PORTAL)
↓ monta o artefato: site/ sem generator/
↓ publica no GitHub Pages
```

Uma falha de publicação não bloqueia desenvolvimento: o workflow não é pré-requisito de nenhum outro, e os produtos nunca dependem do portal.

A publicação de qualquer GitHub Release do repositório também dispara o workflow. Isso evita que a projeção de downloads fique congelada até o próximo commit na `main`. Em eventos `release.published`, o checkout é explicitamente feito da branch padrão e o campo `source.commit` recebe o SHA realmente projetado; a tag da release é apenas fonte de distribuição consultada pela API, nunca autoridade do código do Portal.

**Pré-requisito do proprietário:** em *Settings → Pages*, definir *Source* = **GitHub Actions**.

O workflow valida também o estado efetivo do Pages via API antes de publicar: `build_type` precisa ser `workflow`. Isso evita um falso positivo em que o workflow customizado termina verde, mas a publicação legada da branch continua servindo `README.md`. Antes do upload, o artefato é recusado se não contiver `index.html`, `app.js`, `style.css` e a projeção gerada na raiz esperada, ou se contiver `README.md`. Depois do deploy, um smoke test acessa a URL pública com cache-buster do commit e só considera a publicação válida se o HTML servido contiver o entrypoint do portal.

## 6. Estrutura

```text
site/
├── index.html           estrutura estática, mobile-first (casca da página, navegação, paleta de busca)
├── app.js               busca data/ecosystem-status.json e renderiza; indicadores ao vivo (§8); sem dependências
├── style.css            design system: tokens por papel, tema claro/escuro, páginas de validação
├── generator/           gerador C# da projeção (não publicado)
└── data/                gerado no CI (não versionado)
```

`assets/` será criado quando houver o primeiro asset (ADR-0004: sem diretórios vazios).

Pré-visualização local:

```bash
dotnet run site/generator/GenerateStatus.cs
python3 -m http.server -d site 8000   # qualquer servidor estático
```

## 7. Evolução planejada (ver ROADMAP)

- **Releases e artefatos (P1-9, implementado):** versão (do arquivo de versão do produto no monorepo), última release (tag, data, pré-lançamento e link para as notas na página da release), APK/instalador com tamanho e SHA-256 (campo `digest` da API ou `SHA256SUMS.txt`) de cada produto, lidos na geração da API pública do GitHub dos repositórios de origem (DEC-0008) com `--releases online` no `pages.yml`; **nunca digitados**. Geração offline ou falha da API: "não disponível".
- **Urbe Web (P1-10):** rota previsível para abrir o Urbe Web, decidida durante a auditoria/migração do Urbe sem quebrar sua publicação atual. Não presumida agora.
- **Lunet2D:** o GitHub Pages não executa o Lunet2D; o portal oferece estado, release, APK, checksum, release notes, roteiro de teste, documentação e validações pendentes. Nenhuma versão web falsa.
- **Páginas de validação (P1-11, implementado):** `/testing/<componente>/<build>/`, geradas de registros canônicos de validação, com versão/build, objetivo, pré-condições, passos numerados, resultado esperado, problemas conhecidos, link para o artefato e tarefa/roadmap/commit/PR. Persistência de PASS/FAIL não é objetivo inicial. O gerador (`site/generator/GenerateStatus.cs`) escreve as páginas em `site/testing/` (não versionado, como `site/data/`) a partir de `docs/validation/**/*.json`; o estado de validação de cada componente na projeção é o do registro mais recente, e `CHK-PORTAL` recusa divergência.
- **Hub (P3-1):** a release e o APK são derivados do canal `github-release` do perfil `current`, cuja localização vem de `publicUrl` em `ecosystem.json`. O link desse canal aparece em Releases e na recuperação; não é apresentado como versão Web. A última release publicada do componente (inclusive pré-lançamento) é escolhida por `published_at`, ignorando rascunhos. No próprio monorepo, as tags são filtradas por `<component-id>-v` (convenção do canal do Hub, DEC-0028), para não oferecer a release de outro produto. A consulta cobre as últimas 100 releases do canal; ausência nesse recorte, resposta inválida, geração offline ou falha da API ⇒ "não disponível", mantendo o link de recuperação. APK, tamanho e SHA-256 usam o mesmo contrato de artefatos dos demais Products. Nenhuma release, URL de APK, versão ou checksum é digitado no portal.

Testes de releases: `dotnet run tests/consistency/Check.cs -- --self-test` executa o gerador com respostas gravadas (`--release-fixtures <arquivo>`, mapa de URL de repositório para resposta da API, permitido somente offline). Esse modo declara a origem como resposta gravada e não é usado pelo workflow de publicação. Os testes cobrem tags de outros componentes, rascunhos, pré-lançamento, data publicada, APK/checksum, preservação do Urbe Web, ausência de release, resposta inválida e recuperação offline.

## 8. Interface e indicadores ao vivo (P3-11)

A interface segue o ADR-0005 (HTML/CSS/JS estáticos, sem build, sem dependências e sem fontes ou scripts de terceiros) e é organizada pela pergunta do proprietário, não pela estrutura do repositório:

| Seção | Responde | Fonte |
|-------|----------|-------|
| Cabeçalho | "Algo precisa de mim?" — resumo em uma frase, checks, idade da projeção, estado dos dados ao vivo | projeção + §8.1 |
| Precisa de você | decisões, mudanças críticas prontas e validações críticas, cada uma com o objeto (§3.1–§3.3); validações não críticas recolhidas em "Sem pressa" | projeção; mudanças críticas prontas lidas ao vivo (§8.1), com o instantâneo da projeção como reserva |
| Indicadores | saúde do desenvolvimento em números | §8.1 + projeção |
| Apps | versão, release, downloads (com tamanho), validação humana, espelho, fontes e SHA-256 copiável | projeção (+ espelho ao vivo, já existente) |
| Atividade de integração | integrações por dia (rotina × crítico), fila atual, últimas integrações com o commit exato | §8.1 |
| Roadmap | fases e gates, com o nome e o progresso de cada fase (itens `[x]`, `[~]`, `[ ]`) | projeção (`ecosystem.gates[]`, derivado do ROADMAP; `CHK-PORTAL` compara) |
| Distribuição e recuperação, Documentação | canais, recuperação sem o Hub, candidatos a reutilização, fontes normativas | projeção |

Busca e navegação: `Ctrl K` / `⌘K` (ou `/`) abre uma paleta que procura seções, ações de cada Product (abrir, baixar, roteiro de teste), pendências, PRs abertos, integrações recentes e documentos. O tema (automático, claro, escuro) é preferência local do visitante (`localStorage`), nunca estado do Ecosystem.

### 8.1 Indicadores ao vivo

Lidos **pelo navegador do visitante** da API pública do GitHub, sem token, somente leitura e nunca gravados (mesmo princípio do espelho de distribuição). Cache de 5 minutos por sessão (`sessionStorage`) para respeitar o limite da API pública (60 requisições/hora por IP). Falha de rede ou limite ⇒ o indicador mostra "não disponível" com o motivo; leitura incompleta do histórico ⇒ o número diz "parcial". Nada é inventado nem extrapolado.

| Indicador | Definição | Origem |
|-----------|-----------|--------|
| Integradas sem você | integrações dos últimos 14 dias que entraram sozinhas ÷ todas as integrações do período. "Sozinha" = commit do integrador com `Integration-Criticality: routine`; "com você" = `Integration-Criticality: critical` (autorizada) ou merge pelo botão do GitHub | commits da branch padrão |
| Do PR à main | mediana (e p90) de `merged_at − created_at` dos PRs integrados nos últimos 14 dias | PRs fechados |
| Fila agora | PRs abertos elegíveis para a fila, pelas mesmas regras do integrador (rascunho, fork e Dependabot ficam fora e são contados à parte): esperando você (`critico` + `pronto-para-integrar`), devolvidos ao autor (`precisa-reconciliar`), em teste ou na fila | PRs abertos e suas labels |
| CI da main | execuções concluídas com sucesso ÷ (sucesso + falha) entre as últimas execuções do GitHub Actions na branch padrão | execuções do Actions |
| Gates aprovados, Products ativos | contagens da projeção | projeção |

As cores de dados usam uma paleta categórica validada para daltonismo nos dois temas (rotina = azul, crítico = laranja); cores de status (bom, atenção, crítico) são reservadas e sempre acompanhadas de ícone e rótulo. Todo gráfico tem legenda, detalhe ao passar o mouse ou focar pelo teclado e uma visão em tabela.

Estes indicadores são do **desenvolvimento** (portal humano de desenvolvimento, ADD-0001); não reproduzem o Past / Now / Next nem o control plane do Hub (§1).
