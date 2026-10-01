# Auditoria técnica e de projeto — Urbe 1.x

> Status: diagnóstico de base para o programa Urbe 2.0.
>
> Snapshot auditado: `main` em `91de0f4bc3626ffdf471b81d1b9b7e74ff0fceea` (v1.8.2-beta).
>
> Esta auditoria descreve o estado encontrado. Ela não é ainda a especificação canônica da 2.0 e não autoriza uma reescrita geral.

## 1. Objetivo

Estabelecer uma linha de base verificável antes de planejar a Urbe 2.0, separando:

- capacidades que já funcionam e devem ser preservadas;
- autoridades canônicas já existentes;
- dívida arquitetural e acoplamentos históricos;
- riscos de dados, plataforma, segurança, performance e distribuição;
- falhas de processo que permitem divergência entre intenção, documentação, código e release.

A meta da 2.0 não é apenas reduzir arquivos grandes. É fazer com que a estrutura do repositório, a arquitetura e o processo de desenvolvimento expressem a mesma fonte de verdade.

## 2. Inventário observado

No snapshot auditado:

- 248 arquivos versionados;
- 71 arquivos em `src/`;
- 31 arquivos de teste `tests/*.mjs`;
- 31 branches anteriores `claude/*` ainda presentes, além de `main`;
- a busca de issues retornou zero issues;
- a numeração de PRs chegou ao PR #32;
- `src/app.js`: 438.123 bytes / 5.633 linhas;
- `src/legacy/bootstrap.js`: 97.660 bytes;
- `src/pages/engine.js`: 106.745 bytes;
- `src/pages/studio.js`: 83.982 bytes;
- `src/world/life.js`: 44.431 bytes;
- `src/ai/ui.js`: 44.496 bytes.

Áreas já separadas incluem `core`, `persistence`, `editor`, `explorer`, `world`, `ai`, `pages`, `customize`, `composition`, `math`, `native` e `ui`.

## 3. Pontos fortes que a 2.0 deve preservar

### 3.1 Princípios arquiteturais já explícitos

O `ARCHITECTURE.md` atual já define princípios valiosos:

- local-first e offline-first;
- `DocumentStore` como autoridade do conteúdo;
- identidade documental estável;
- cidade/aquário como projeção;
- mobile-first;
- persistência recuperável;
- migração incremental em vez de novos wrappers históricos.

A 2.0 deve consolidar esses princípios, não descartá-los.

### 3.2 Núcleo canônico existente

Há serviços reais e utilizáveis, não apenas intenção documental:

- `UrbeCore` com EventBus, CommandRegistry, StateStore e registro de serviços;
- `DocumentStore`;
- `KnowledgeIndex`;
- histórico e lixeira;
- `WorkspacePersistence`;
- `WorldProjection` e `AquariumWorld`;
- serviços do editor, explorer, IA, páginas, personalização e composição.

Isso permite migração incremental sem reescrever tudo.

### 3.3 Persistência e proteção de dados

A persistência já possui:

- journal para operações multi-arquivo;
- recuperação de journal na abertura;
- histórico e lixeira persistidos no vault;
- gravações externas reconciliadas com edição local pendente;
- bridge nativa com validação de caminhos;
- Electron com `contextIsolation: true`, `sandbox: true` e `nodeIntegration: false`;
- checagem de origem para IPC;
- Android validando caminhos canônicos nas leituras em lote.

### 3.4 Testes e CI

O repositório já tem uma base útil:

- 31 testes Node;
- checagem de sintaxe de todos os `.js`;
- verificação do Tutorial gerado;
- teste de consistência entre `src`, `index.html` e `sw.js`;
- testes dedicados a persistência, recuperação, bridge nativa, Android, cidade, terreno, IA, editor, explorer, páginas e personalização.

A deficiência principal não é “não ter testes”; é transformar os testes em gates explícitos de requisitos, arquitetura, compatibilidade e release.

## 4. Achados

### AUD-001 — Centro histórico ainda é autoridade operacional

**Severidade:** alta.

`src/app.js` continua sendo o maior arquivo da aplicação e registra serviços como:

- `legacy.runtime`;
- `legacy.documents`;
- `world.custom`;
- `city.layout`;
- `world.life.host`.

Também consulta diretamente numerosos serviços do Core e mantém integração de editor, cidade, persistência, importação, menus, desenho e compatibilidade.

**Risco:** a modularização existe nas bordas, mas novas capacidades ainda podem acabar no centro histórico. Isso mantém autoridades concorrentes e torna mudanças locais dependentes do estado implícito do monólito.

**Direção 2.0:** `app.js` deve convergir para composição/bootstrap de aplicação e adapters de compatibilidade, sem possuir regras de domínio.

### AUD-002 — O monólito cresceu por sobreposição histórica

**Severidade:** alta.

Dentro de `src/app.js`, `V21_VERSION` aparece sendo atribuído sucessivamente a `0.21.0`, `0.22.0`, `0.23.0` e finalmente `1.8.2-beta`.

Isso mostra que parte relevante do arquivo preserva camadas de implementação acumuladas por versão.

**Risco:** funções e patches históricos podem permanecer ativos por ordem de execução em vez de por contratos explícitos.

**Direção 2.0:** nenhuma nova feature deve ser implementada como “camada de versão” anexada ao monólito. A migração deve substituir uma autoridade por vez e remover a implementação substituída.

### AUD-003 — Dependências são determinadas por ordem global de scripts

**Severidade:** alta.

O app não possui etapa de build e carrega dezenas de scripts diretamente pelo `index.html`. Os módulos usam IIFEs e publicam/consomem objetos em `window`, por exemplo `UrbeCore`, `UrbeDialogs`, `UrbeNative`, `UrbePlugins`.

A ordem em `index.html` é, portanto, parte do contrato de runtime.

**Risco:** dependências inválidas só aparecem em runtime; boundaries arquiteturais não são verificadas estaticamente; renomear/mover um módulo pode alterar inicialização sem mudança semântica aparente.

**Direção 2.0:** decidir formalmente entre manter o runtime sem build com um sistema explícito de módulos/contratos, ou adotar módulos ES/build. A decisão precisa de ADR e plano de migração; não deve ser tomada apenas para “modernizar”.

### AUD-004 — Arquitetura canônica e adapters legacy coexistem

**Severidade:** alta.

O próprio `ARCHITECTURE.md` diz que interfaces legadas devem delegar ao Core. O estado atual já faz isso em vários lugares, porém `app.js` ainda fornece `legacy.documents` e `legacy.runtime`, e código de UI consulta esses serviços.

**Risco:** duas formas válidas de executar a mesma ação aumentam regressões e tornam difícil saber onde colocar mudanças.

**Direção 2.0:** inventariar cada adapter legacy com: consumidores, autoridade canônica substituta, teste de produção, condição de remoção e versão de remoção.

### AUD-005 — A fonte de verdade da versão é duplicada

**Severidade:** média/alta.

A versão aparece em múltiplos lugares, incluindo:

- `package.json`;
- `src/core/core.js`;
- `src/app.js`;
- `index.html`;
- `sw.js`;
- testes.

O teste `tests/consistency.mjs` obtém a última ocorrência de `V21_VERSION`, portanto confirma a versão final, mas não impede as atribuições históricas anteriores. `tests/static.mjs` também contém a versão atual hardcoded.

O README ainda anuncia “Versão 1.7 beta” no snapshot 1.8.2-beta.

**Risco:** drift documental e releases inconsistentes.

**Direção 2.0:** uma fonte de versão; demais superfícies devem ser derivadas ou validadas contra ela sem hardcode repetido.

### AUD-006 — Integração em main e publicação estão acopladas

**Severidade:** alta para o processo.

O workflow `.github/workflows/app.yml` verifica a versão em `package.json` e, em push para `main`, publica um GitHub Release caso `v<versão>` ainda não exista.

**Risco:** um merge de integração pode virar publicação; bump de versão passa a ter efeito operacional amplo; preparação de release, estabilização e publicação não possuem gates separados.

**Direção 2.0:** separar integração contínua de release. O mecanismo exato (tag, branch de release ou workflow manual aprovado) será decidido na especificação.

### AUD-007 — Planejamento está concentrado em PRs, não em requisitos

**Severidade:** alta para governança.

Os PRs recentes são detalhados e frequentemente contêm problema, solução, medições e testes. Porém não há backlog de issues ativo no snapshot.

**Risco:** o “por quê” e os critérios ficam presos ao PR depois que a implementação já existe. Não há rastreabilidade estável requisito → decisão → trabalho → teste.

**Direção 2.0:** toda mudança não trivial deve nascer de requisito/issue ou ADR rastreável antes do código.

### AUD-008 — Branches antigas não têm política de ciclo de vida visível

**Severidade:** média.

Há 31 branches `claude/*` além de `main`.

**Risco:** ruído operacional e dificuldade de distinguir trabalho ativo, abandonado e já incorporado.

**Direção 2.0:** branches são temporárias; histórico permanente vive em commits, PRs, issues, requisitos e ADRs. Limpeza deve ocorrer somente após verificar incorporação ou descarte consciente.

### AUD-009 — Testes de integração relatados em PRs não estão formalizados no pipeline

**Severidade:** média/alta.

PRs recentes relatam validações com Electron real, Playwright e emulação de Pixel. O `package.json` e os workflows atuais não expõem uma suíte E2E equivalente como comando/gate reproduzível do repositório.

**Risco:** conhecimento de validação fica no contexto do agente/PR e não pode ser reproduzido por outro executor.

**Direção 2.0:** classificar testes em unitário/contrato/integração/E2E/performance/manual e tornar cada gate reproduzível.

### AUD-010 — O limite “documento” mistura notas com outros artefatos textuais

**Severidade:** média/alta.

`WorkspacePersistence.load` inclui no `DocumentStore` extensões como Markdown, TXT, HTML, JS, CSS, JSON, YAML e CSV, exceto caminhos ocultos. Isso permite tratar plugins, configurações e outros artefatos textuais pelo mesmo mecanismo de documentos.

**Risco:** regras de nota, página, plugin, configuração e conteúdo podem compartilhar uma autoridade que não distingue tipos semanticamente.

**Direção 2.0:** especificar o modelo de artefatos do vault. “Tudo é arquivo” pode continuar verdadeiro sem exigir que “todo arquivo textual é uma nota”.

### AUD-011 — Identidade estável existe, mas metadados espaciais ainda são indexados por path

**Severidade:** alta para a evolução do modelo de dados.

`DocumentStore` usa ID estável e preserva identidade em rename/move. Porém `WorldProjection` mantém `spatial` indexado por path e migra a chave quando recebe `document:updated`. O `.urbe/mapa.json` também materializa `notas` por path, embora armazene o ID dentro do item.

**Risco:** path continua participando da identidade de metadados, aumentando complexidade de rename, merge, sincronização e futuras migrações.

**Direção 2.0:** o formato canônico novo deve preferir ID para relações internas e manter compatibilidade de leitura com mapas 1.x baseados em path.

### AUD-012 — Plugins possuem integridade/aprovação, não isolamento

**Severidade:** média/alta de segurança.

`src/customize/plugins.js` declara explicitamente que plugin é “código de verdade, com o mesmo poder do app”. O código é executado por `new Function`. Há aprovação local por hash do código, bloqueio de autoexecução após alteração e API com limpeza automática.

**Risco:** aprovação por hash protege contra mudança silenciosa, mas não cria sandbox. Um plugin aprovado continua executando no contexto da aplicação.

**Direção 2.0:** decidir explicitamente o modelo de confiança: plugins full-trust aprovados ou plugins isolados/capability-based. A documentação e os testes devem corresponder ao modelo escolhido.

### AUD-013 — Segurança nativa tem boas medidas, mas não há threat model unificado

**Severidade:** média.

Electron e Android já possuem controles concretos de origem e caminho. IA, plugins, HTML exportável, filesystem, links externos e bridges nativas, entretanto, são superfícies distintas sem um documento único de ameaças e trust boundaries.

**Direção 2.0:** criar modelo de ameaças e requisitos verificáveis para cada boundary.

### AUD-014 — Existem outros hotspots além de app.js

**Severidade:** média.

`pages/engine.js`, `pages/studio.js`, `pages/free.js`, `ai/ui.js`, `world/life.js`, `customize/panel.js`, `world/pixel-art.js` e folhas CSS grandes também merecem análise de responsabilidade.

**Risco:** focar somente em `app.js` pode deslocar o monólito para outros subsistemas.

**Direção 2.0:** usar métricas de responsabilidade/acoplamento e não apenas tamanho de arquivo para priorizar extrações.

### AUD-015 — Compatibilidade do vault precisa virar contrato versionado

**Severidade:** alta.

Há versões locais de estruturas específicas, como o journal `version: 1`, e o código lê formatos 1.x de forma defensiva. Porém a compatibilidade global do vault não está expressa como política de schema/migração com gates.

**Risco:** mudanças futuras em mapa, páginas, personalização, histórico, plugins e composição podem criar migrações independentes e difíceis de coordenar.

**Direção 2.0:** catálogo de formatos persistidos, versões, política de migração forward/backward e fixtures de vaults históricos.

### AUD-016 — Repositório público sem política de licença consolidada

**Severidade:** média para distribuição.

O repositório é público, mas `package.json` usa `UNLICENSED` e não há licença declarada no repositório.

**Risco:** intenção de distribuição, colaboração e direitos de reutilização ficam ambíguos.

**Direção 2.0:** decidir conscientemente a política de licença antes de ampliar distribuição/contribuição externa.

## 5. Riscos sistêmicos

Os achados se agrupam em quatro riscos centrais:

1. **Autoridade:** ainda existem caminhos canônicos e históricos para a mesma responsabilidade.
2. **Rastreabilidade:** boas decisões aparecem em PRs, mas não sobrevivem como requisitos normativos.
3. **Compatibilidade:** o Urbe já persiste muitos formatos; a 2.0 não pode tratá-los como detalhe de implementação.
4. **Release:** desenvolvimento, versão e publicação estão mais acoplados do que o desejável para uma fase de migração grande.

## 6. Regras para a migração 2.0 derivadas da auditoria

Até a SPEC 2.0 ser aprovada:

- não fazer reescrita geral;
- não mover arquivos apenas para “organizar”;
- não criar novas camadas de versão dentro de `app.js`;
- não adicionar nova autoridade quando já existir serviço canônico;
- corrigir bugs críticos da 1.x normalmente, mas registrar impacto em compatibilidade;
- toda extração de legacy deve ter teste que prove a autoridade substituta;
- nenhuma mudança de formato persistido sem estratégia de migração;
- nenhuma grande feature da 2.0 antes de estar representada no Requirement Ledger.

## 7. Próximas auditorias necessárias

Esta primeira auditoria é estrutural. Antes da SPEC canônica ainda faltam auditorias focalizadas:

- mapa de dependências e responsabilidades por subsistema;
- catálogo completo de formatos persistidos;
- superfície de comandos/eventos/serviços;
- inventário de adapters legacy e consumidores;
- matriz de testes × capacidades;
- baseline de performance em vault pequeno/médio/grande;
- threat model;
- inventário de UX e fluxos de usuário;
- compatibilidade Windows/Android/Web;
- política de release/versionamento/licença.

Esses itens alimentam a descoberta; eles não devem ser comprimidos em uma única tarefa genérica no roadmap final.
