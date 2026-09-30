# Requirement Ledger — Urbe 2.0

> Status: descoberta concluída (issue #33); ledger com REQ-001..REQ-088. SPEC e ROADMAP aprovados pelo proprietário em 2026-09-30.
>
> Este ledger mantém identidades estáveis para decisões e requisitos. Ele pode crescer. Itens não desaparecem silenciosamente.

## Estados

- **IMPLEMENTAR** — requisito aceito para a 2.0.
- **ADIADO** — válido, mas fora da entrega 2.0 atual.
- **REJEITADO** — avaliado e recusado, com justificativa.
- **SUBSTITUÍDO** — substituído explicitamente por outro requisito.
- **FORA DE ESCOPO** — não pertence ao programa.

## Requisitos consolidados

| ID | Classe | Estado | Requisito |
|---|---|---|---|
| REQ-001 | ARCH | IMPLEMENTAR | A Urbe 2.0 deve possuir arquitetura alvo explícita, com responsabilidades e dependências permitidas/proibidas. |
| REQ-002 | QUALITY | IMPLEMENTAR | Mudanças relevantes devem ser rastreáveis de uma necessidade/requisito até decisão, implementação, teste e gate. |
| REQ-003 | ARCH | IMPLEMENTAR | `app.js` e código legacy devem perder autoridades funcionais por migração incremental, nunca por reescrita cega. |
| REQ-004 | QUALITY | IMPLEMENTAR | Documentação normativa, código, testes e releases devem permanecer sincronizados automaticamente sempre que possível. |
| REQ-005 | QUALITY | IMPLEMENTAR | O projeto deve possuir Definition of Done verificável: implementação, integração, testes, documentação, aceite e ausência de regressão conhecida. |
| REQ-006 | DISTRIBUTION | IMPLEMENTAR | Integração em `main` e publicação de release devem possuir gates separados. |
| REQ-007 | DATA | IMPLEMENTAR | Local-first/offline-first e compatibilidade dos dados existentes são invariantes durante a migração. |
| REQ-008 | PLATFORM | IMPLEMENTAR | Web, Windows e Android devem compartilhar a aplicação; diferenças de plataforma ficam atrás de contratos/adapters explícitos. |
| REQ-009 | PERFORMANCE | IMPLEMENTAR | Performance deve possuir baselines, budgets, cenários reproduzíveis e gates para regressões relevantes. |
| REQ-010 | SECURITY | IMPLEMENTAR | Plugins, IA, filesystem, HTML/conteúdo ativo e bridges nativas devem possuir trust boundaries e requisitos de segurança documentados/testáveis. |
| REQ-011 | UX | IMPLEMENTAR | Cidade, editor/PKM, IA, páginas, plugins e UX continuam no escopo da 2.0, mas novas mudanças entram por requisitos explícitos. |
| REQ-012 | ARCH | IMPLEMENTAR | Novas features não podem ampliar silenciosamente a dívida que a 2.0 pretende remover. |
| REQ-013 | DATA | IMPLEMENTAR | Relações e metadados internos novos devem preferir IDs documentais estáveis; compatibilidade com formatos 1.x baseados em path deve ser preservada. |
| REQ-014 | DATA | IMPLEMENTAR | O vault deve ter um modelo explícito de tipos de artefato (nota, página, plugin, configuração, composição, asset etc.) sem perder a propriedade de arquivos comuns. |
| REQ-015 | QUALITY | IMPLEMENTAR | Boundaries arquiteturais e contratos de inicialização devem ter validação automatizada, não depender apenas de convenção. |
| REQ-016 | QUALITY | IMPLEMENTAR | Testes de integração/E2E relevantes devem ser reproduzíveis a partir do repositório e associados aos gates que cobrem. |
| REQ-017 | DISTRIBUTION | IMPLEMENTAR | A política de licença e distribuição deve ser decidida explicitamente antes da ampliação pública da 2.0. |
| REQ-018 | QUALITY | IMPLEMENTAR | O repositório deve possuir política de issues, branches, PRs, ADRs e limpeza de branches com ciclo de vida definido. |
| REQ-019 | ARCH | IMPLEMENTAR | A versão do produto deve possuir uma única fonte de verdade; demais representações devem ser derivadas ou validadas sem hardcodes concorrentes. |
| REQ-020 | SECURITY | IMPLEMENTAR | O modelo de confiança de plugins deve ser uma decisão explícita: full-trust aprovado ou isolamento/capabilities, com documentação coerente. |
| REQ-021 | SECURITY | IMPLEMENTAR | Armazenamento e exposição de credenciais de provedores de IA devem ter threat model e política de proteção compatíveis com cada plataforma. |
| REQ-022 | ARCH | IMPLEMENTAR | Hotspots fora de `app.js` devem ser avaliados por responsabilidade e acoplamento antes de decidir extrações. |
| REQ-023 | DATA | IMPLEMENTAR | Todos os formatos persistidos do vault devem ser catalogados, versionados quando necessário e cobertos por política/fixtures de migração. |
| REQ-024 | QUALITY | IMPLEMENTAR | A especificação canônica e o roadmap final só podem ser aprovados após auditoria de cobertura REQ → SPEC → roadmap → teste/gate. |

## Requisitos derivados da descoberta (issue #33)

Todos os REQ-001..024 acima permanecem intactos e IMPLEMENTAR. Os requisitos abaixo concretizam achados de `docs/v2/discovery/*` e das decisões do proprietário (`docs/v2/adr/`). Coluna **Origem**: achado (AUD-xxx do AUDIT-1X, R-x de DATA-CATALOG, T-x de THREAT-MODEL, L-x de LEGACY-MAP, G-x de TEST-MATRIX/PERFORMANCE), REQ refinado e ADR.

### Arquitetura

| ID | Classe | Estado | Requisito | Origem |
|---|---|---|---|---|
| REQ-025 | ARCH | IMPLEMENTAR | Sem build obrigatório: existirá um registro/manifesto único de módulos (nome, provê, requer, ordem) do qual `index.html`, `sw.js`, `tools/build-www.mjs` e testes são derivados ou validados; boundaries e ordem de inicialização são verificados automaticamente. | AUD-003; refina REQ-015; ADR-0001 |
| REQ-026 | ARCH | IMPLEMENTAR | Renderer, touch e grafo de ruas canônicos do aquário (`aquarium.renderer/touch/roads`) devem ser ligados ao runtime de produção, e o desenho/interação duplicados em `app.js` removidos após teste de produção. | L6; refina REQ-003 |
| REQ-027 | ARCH | IMPLEMENTAR | A superfície do editor hoje em `app.js` (renderMarkdown, editor Visual, autocompletar de wikilinks, barra de formatação) deve migrar para `src/editor` com contrato e testes de comportamento. | ARCHITECTURE-MAP §3; refina REQ-003 |
| REQ-028 | ARCH | IMPLEMENTAR | `FS`/`Disco`/`DBK` de `app.js` devem virar adapters de persistência (`src/persistence/adapters/*`) atrás de um contrato único, com `WorkspacePersistence` como única autoridade de escrita do vault. | L5; R-5 |
| REQ-029 | ARCH | IMPLEMENTAR | Cadeias de sobrescrita e camadas de versão de `app.js` (`estadoDesejado`×5, `abrirCidade`×4, `drawTrees`×5, `rebuildRoadNetwork`×3, funções duplicadas) devem ser eliminadas; `app.js` converge para composição/bootstrap sem regra de domínio. | AUD-001, AUD-002; refina REQ-003 |
| REQ-030 | ARCH | IMPLEMENTAR | Cada adapter legacy L1–L4, L7, L8, L10 e L11 deve ser removido quando cumprir sua condição de remoção (LEGACY-MAP), com teste de produção provando a autoridade substituta e sem consumidor remanescente. | AUD-004; LEGACY-MAP |
| REQ-031 | ARCH | IMPLEMENTAR | JSZip embutido em `src/legacy/bootstrap.js` deve ser movido para `vendor/jszip/` (com licença), atualizando `index.html`, `sw.js`, testes e contagens; o diretório `legacy/` deve conter apenas adapters reais. | L0 (LEGACY-MAP) |
| REQ-032 | ARCH | IMPLEMENTAR | Contratos públicos consumidos por terceiros (API de plugins `urbe.*`, serviço `world.custom`, comandos e eventos estáveis) devem ser versionados e documentados antes de qualquer mudança incompatível. | L7; R-7 |
| REQ-033 | ARCH | ADIADO | Launcher que centralize múltiplos aplicativos do proprietário. Justificativa: ideia futura declarada pelo proprietário; a 2.0-beta permanece simples; nenhuma decisão de 2.0 deve impedi-lo. Reavaliar após 2.0. | Decisão do proprietário; ADR-0001 |
| REQ-034 | ARCH | ADIADO | Migração para ES modules nativos ou bundler. Justificativa: decisão do proprietário de manter runtime sem build na 2.0-beta; REQ-025 entrega verificação sem trocar o modelo. Reavaliar junto com REQ-033. | ADR-0001 |

### Dados

| ID | Classe | Estado | Requisito | Origem |
|---|---|---|---|---|
| REQ-035 | DATA | IMPLEMENTAR | Proteção forward: nenhum leitor pode descartar, sobrescrever ou apagar arquivo `.urbe/*` (journal, history, trash, compositions, mapa, tema, páginas) cuja versão seja desconhecida/maior; nesses casos o app preserva o arquivo, avisa e opera em modo seguro/somente leitura para aquele artefato. | R-1, R-14; AUD-015; ADR-0004 |
| REQ-036 | DATA | IMPLEMENTAR | O vault deve declarar sua versão de formato (`.urbe/vault.json`: `formatVersion`, `createdBy`, `lastWriter`, `migrations[]`), lida antes de qualquer escrita. | AUD-015; ADR-0004 |
| REQ-037 | DATA | IMPLEMENTAR | Deve existir um conjunto versionado de fixtures de vaults históricos (mapa v1/v2/v4, com e sem `id`, com journal pendente, `Cidades/` e `.urbe/origens/`, `tema.json`, páginas, plugins, composições) e testes de migração/abertura sobre eles. | R-2, R-13; REQ-023 |
| REQ-038 | DATA | IMPLEMENTAR | Toda migração destrutiva ou de formato deve ser precedida de backup restaurável dentro do vault (`.urbe/backup/<data>-<de>-<para>/`) e ser idempotente. | ADR-0004; REQ-007 |
| REQ-039 | DATA | IMPLEMENTAR | Modelo de artefatos do vault com fonte única de tipos e extensões (nota, página, plugin, tema, estilo, textura, composição, asset, sistema), substituindo as cinco listas divergentes de extensões; artefatos não-nota não entram no DocumentStore como nota por engano. | AUD-010, R-11; implementa REQ-014; ADR-0006 |
| REQ-040 | DATA | IMPLEMENTAR | `.urbe/mapa.json` deve ter um único escritor (`WorkspacePersistence`), `mapa.v` deve ser lido e validado, e o segundo caminho de escrita (`rodarSinc`) eliminado. | R-5; §0 DATA-CATALOG |
| REQ-041 | DATA | IMPLEMENTAR | Regiões, construções/assets e vínculos por nome (`parentNoteName`) devem receber IDs estáveis nos formatos novos, mantendo leitura de referências por path/nome da 1.x. | R-3, R-6; AUD-011; implementa REQ-013; ADR-0006 |
| REQ-042 | DATA | IMPLEMENTAR | Identidade documental deve sobreviver a edição externa (rename/move por fora, sincronizadores, cópia do vault): reconciliação por sidecar em `.urbe/` (sem injetar ID no arquivo do usuário) e coleta de órfãos em history/trash/compositions. | R-2; ADR-0006 |
| REQ-043 | DATA | IMPLEMENTAR | Versão de mundo explícita e migração de terreno/layout que nunca reposicione a cidade do usuário sem backup e desfazer; vaults mesclados sem `mundo` recebem tratamento definido. | R-4 |
| REQ-044 | DATA | IMPLEMENTAR | Exportação ZIP deve incluir manifesto versionado (formato, versão do app, lista, hashes) e oferecer exportar/importar o estado local relevante (aprovações de plugins, preferências), nunca chaves de IA. | R-8, R-9 |
| REQ-045 | DATA | IMPLEMENTAR | Política definida e testada para `Cidades/` e `.urbe/origens/` gerados pela migração multi-cidade da 1.x (idempotência, sem duplicação a cada boot, opção de arquivar). | R-13 |
| REQ-046 | DATA | IMPLEMENTAR | Escrita atômica (ou recuperável) também na web/FSA, e integridade + export garantido para o modo IndexedDB interno (único dado do usuário fora de arquivo real). | R-10, R-15 |
| REQ-047 | DATA | IMPLEMENTAR | Estado local que referencia documentos (`urbe.editor.workspace.v1`, favoritos/recentes, aprovações de plugins) deve ser escopado por vault e tolerar IDs órfãos. | R-9; §1.2 DATA-CATALOG |
| REQ-048 | DATA | IMPLEMENTAR | O campo `aiLocal` do mapa e demais campos persistidos sem consumidor devem ser auditados: consumir, migrar ou remover com política de compatibilidade documentada. | R-12 |
| REQ-049 | DATA | IMPLEMENTAR | Normalização de páginas/temas/blocos deve preservar chaves desconhecidas e não reescrever `version` silenciosamente; abrir e salvar com versão mais antiga não pode perder dados. | R-14 |

### Segurança

| ID | Classe | Estado | Requisito | Origem |
|---|---|---|---|---|
| REQ-050 | SECURITY | IMPLEMENTAR | Content-Security-Policy documentada, aplicada e testada em web/PWA, Electron (`app://urbe`) e Capacitor, compatível com plugins aprovados e exports. | T-1 |
| REQ-051 | SECURITY | IMPLEMENTAR | UX honesta de plugins full-trust: antes de aprovar, mostrar código, hash SHA-256 e alcance real (rede, chaves, filesystem); SHA-256 obrigatório (sem fallback FNV); documentação e testes coerentes com o modelo. | AUD-012; concretiza REQ-020; ADR-0002 |
| REQ-052 | SECURITY | ADIADO | Isolamento de plugins por capabilities (Worker/iframe). Justificativa: decisão do proprietário por full-trust aprovado; isolamento quebra plugins 1.x e amplia escopo; reavaliar com contrato de API versionado (REQ-032). | ADR-0002 |
| REQ-053 | SECURITY | IMPLEMENTAR | Mitigação de chaves de provedores de IA: `allowBackup=false` no Android, exclusão de export/backup, máscara na UI, aviso de risco e documentação no threat model. | REQ-021; T-IA |
| REQ-054 | SECURITY | ADIADO | Armazenamento criptografado nativo de chaves de IA (safeStorage/Keystore). Justificativa: custo/paridade entre plataformas; mitigações imediatas em REQ-053; reavaliar após E2E nativo. | REQ-021 |
| REQ-055 | SECURITY | IMPLEMENTAR | Hardening do Electron: permission handler condicionado à origem do requisitante, protocolo `app://` com allowlist e validação de host, `printToPDF` sem `file://` privilegiado, IPC guard estrito, hooks de teste inertes em produção. | T-Electron |
| REQ-056 | SECURITY | IMPLEMENTAR | Hardening do Android: `printHtml` sem compartilhar a origem do app (ou sem JS), FileProvider com paths mínimos, validação canônica de caminho no lado Java em todas as operações, `allowBackup=false`. | T-Android |
| REQ-057 | SECURITY | IMPLEMENTAR | HTML/embeds: `sandbox` sem `allow-same-origin` quando conteúdo for de origem controlada por terceiros ou do app, recursos de CDN (PDF.js, KaTeX, fontes) fixados com SRI ou vendorizados; testes de regressão. | T-HTML |
| REQ-058 | SECURITY | IMPLEMENTAR | Importação de ZIP/arquivos com limite de tamanho e contagem, rejeição de entradas absolutas ou com `..` e testes de zip-slip/zip-bomb nas três plataformas. | T-Import |
| REQ-059 | SECURITY | ADIADO | Assinatura do instalador Windows e verificação de integridade do updater além do `sha512` do release. Justificativa: exige certificado e custo; distribuição beta; aviso SmartScreen documentado (REQ-082). | T-Updater |
| REQ-060 | SECURITY | IMPLEMENTAR | Política para artefatos ativos criados por IA (`.js`, `.css`, `.html`): confirmação explícita, sem execução automática, CSS sem requisições externas não aprovadas, aviso de prompt injection e testes. | T-IA |

### Qualidade e processo

| ID | Classe | Estado | Requisito | Origem |
|---|---|---|---|---|
| REQ-061 | QUALITY | IMPLEMENTAR | Suíte de integração/E2E reproduzível (`npm run test:e2e`: navegador real e smoke do Electron) associada aos gates que cobre. | AUD-009; concretiza REQ-016 |
| REQ-062 | QUALITY | IMPLEMENTAR | Testes para `native/desktop/main.js`, `preload.js`, `UrbeAndroidPlugin.java` (JUnit) e Service Worker real, hoje sem cobertura. | TEST-MATRIX §3 |
| REQ-063 | QUALITY | IMPLEMENTAR | `npm test` multiplataforma (runner Node, sem shell POSIX) que roda todos os testes e agrega falhas. | TEST-MATRIX §1 |
| REQ-064 | QUALITY | IMPLEMENTAR | Testes que dependem de fatiar `app.js` por texto ou checar strings devem ser substituídos por testes de comportamento conforme o código migra. | TEST-MATRIX §2 |
| REQ-065 | QUALITY | IMPLEMENTAR | Gate de versão única: script que sincroniza/valida `package.json`, `core.js`, `index.html`, `sw.js`, CHANGELOG e testes a partir de uma fonte, eliminando atribuições múltiplas de `V21_VERSION`. | AUD-005; concretiza REQ-019 |
| REQ-066 | QUALITY | IMPLEMENTAR | CI/release: publicação só por tag ou `workflow_dispatch` aprovado, dependente de testes verdes; `permissions` mínimas, actions fixadas por SHA, Dependabot. | AUD-006; concretiza REQ-006; ADR-0005 |
| REQ-067 | QUALITY | IMPLEMENTAR | Ciclo de vida das branches: limpeza das branches `claude/*` após verificação de incorporação, issues como backlog, ADRs e PRs rastreáveis. | AUD-007, AUD-008; concretiza REQ-018 |
| REQ-068 | QUALITY | IMPLEMENTAR | Verificador automatizado de rastreabilidade (REQ → SPEC → ROADMAP → TRACEABILITY) executado no CI. | concretiza REQ-002, REQ-024 |
| REQ-069 | QUALITY | IMPLEMENTAR | Smoke em CI do instalador Windows e do APK (o app empacotado deve abrir e carregar um vault de fixture). | TEST-MATRIX §3 |

### Performance

| ID | Classe | Estado | Requisito | Origem |
|---|---|---|---|---|
| REQ-070 | PERFORMANCE | IMPLEMENTAR | Harness reproduzível de baseline (vaults sintéticos de 50/500/5000 notas; abertura, digitação, salvamento, busca, cidade, memória) com resultados versionados. | PERFORMANCE §4; concretiza REQ-009 |
| REQ-071 | PERFORMANCE | IMPLEMENTAR | Budgets numéricos por classe de dispositivo, definidos a partir do baseline (REQ-070) e verificados como gates de regressão. | REQ-009 |
| REQ-072 | PERFORMANCE | IMPLEMENTAR | Persistência incremental: cada flush escreve apenas o que mudou, sem recomputar e reserializar o vault inteiro. | PERFORMANCE §2 |
| REQ-073 | PERFORMANCE | IMPLEMENTAR | Busca (quick-open e conhecimento) com índice pré-normalizado e debounce; custo independente do tamanho do texto por consulta. | PERFORMANCE §2 |
| REQ-074 | PERFORMANCE | IMPLEMENTAR | Carga inicial: módulos pesados (Studio, IA, KaTeX, conteúdo do tutorial) carregados sob demanda, mantendo runtime sem build; load do vault paralelizado. | PERFORMANCE §2 |
| REQ-075 | PERFORMANCE | IMPLEMENTAR | Histórico com custo limitado: `history.json` com teto de tamanho/compactação e gravação incremental. | PERFORMANCE §2 |

### Plataformas

| ID | Classe | Estado | Requisito | Origem |
|---|---|---|---|---|
| REQ-076 | PLATFORM | IMPLEMENTAR | Contrato `UrbeNative` versionado, documentado e testado por adapter (web, Electron, Android). | PLATFORMS §2; concretiza REQ-008 |
| REQ-077 | PLATFORM | ADIADO | Substituir `MANAGE_EXTERNAL_STORAGE` por SAF/pasta escolhida no Android. Justificativa: Play Store não é alvo; mudança grande de storage; reavaliar quando distribuição mudar. | PLATFORMS |
| REQ-078 | PLATFORM | ADIADO | Pipelines macOS/Linux. Justificativa: alvos declarados são Web, Windows e Android; `build.linux` existe sem pipeline. | PLATFORMS |
| REQ-079 | PLATFORM | IMPLEMENTAR | Paridade de exportação/download entre plataformas (Electron sem `saveFile` equivalente ao Android/web). | PLATFORMS §4 |

### Distribuição

| ID | Classe | Estado | Requisito | Origem |
|---|---|---|---|---|
| REQ-080 | DISTRIBUTION | IMPLEMENTAR | Licença de todos os direitos reservados (`LICENSE`), `THIRD-PARTY-NOTICES` (KaTeX, JSZip, pako, PDF.js, Electron/Chromium, Capacitor) e `package.json` coerente (`UNLICENSED`/`SEE LICENSE IN LICENSE`). | concretiza REQ-017; ADR-0003 |
| REQ-081 | DISTRIBUTION | IMPLEMENTAR | Política de versionamento e canais (SemVer, beta/stable), release notes derivadas do CHANGELOG e procedimento de rollback documentados e testados. | ADR-0005 |
| REQ-082 | DISTRIBUTION | IMPLEMENTAR | Caminho de migração 1.x → 2.x documentado (leitura total da 1.x, escrita 2.x com proteção forward, rollback via backup) e comunicado no app/tutorial, incluindo aviso SmartScreen/APK. | ADR-0004 |

### Produto e UX

| ID | Classe | Estado | Requisito | Origem |
|---|---|---|---|---|
| REQ-083 | UX | IMPLEMENTAR | Critérios de sucesso da 2.0 mensuráveis (abrir e escrever sem perda, recuperação, tempo até primeira nota, ausência de regressão de dados, tarefas críticas por plataforma) publicados e vinculados a gates. | PRODUCT-UX §3 |
| REQ-084 | UX | IMPLEMENTAR | Tutorial e documentação de usuário sincronizados com cada release (o `build-tutorial --check` passa a cobrir novos fluxos, segurança de plugins e migração). | PRODUCT-UX |
| REQ-085 | UX | IMPLEMENTAR | Diagnóstico, modo seguro e recuperação (journal, lixeira, versões, backup de migração) acessíveis na UI e documentados. | PRODUCT-UX |
| REQ-086 | UX | IMPLEMENTAR | Linha de base de acessibilidade (teclado, contraste, foco, leitores de tela nos fluxos essenciais) com requisitos verificáveis. | PRODUCT-UX §5 |
| REQ-087 | UX | FORA DE ESCOPO | Internacionalização além de pt-BR. Justificativa: sem demanda declarada; a 2.0 concentra dados, arquitetura e segurança; strings centralizadas não são pré-requisito. Reavaliar após 2.0. | PRODUCT-UX |

### Governança de agentes

| ID | Classe | Estado | Requisito | Origem |
|---|---|---|---|---|
| REQ-088 | QUALITY | IMPLEMENTAR | `AGENTS.md` (contrato de agentes) e `AGENTSCHAT.md` (log de coordenação/handoff) na raiz, referenciados por CONTRIBUTING e docs/v2, mantidos atualizados a cada sessão de trabalho. | Pedido do proprietário |

## Decisões abertas — status após a descoberta

| Decisão | Status | Onde |
|---|---|---|
| Runtime sem build vs ES modules/bundler | **CONSOLIDADA**: sem build na 2.0-beta; launcher futuro ADIADO (REQ-033, REQ-034) | ADR-0001 |
| Modelo final de plugins | **CONSOLIDADA**: full-trust aprovado com UX honesta; isolamento ADIADO (REQ-051, REQ-052) | ADR-0002 |
| Política de licença | **CONSOLIDADA**: todos os direitos reservados, tudo do proprietário (REQ-080) | ADR-0003 |
| Mecanismo exato de release | **CONSOLIDADA (aprovada 2026-09-30)**: tag/workflow manual com `needs` de testes (REQ-066, REQ-081) | ADR-0005 |
| Estrutura física de pastas | **CONSOLIDADA (direção)**: convergência por subsistema, sem mover em massa; movimentos só junto com o REQ que os exige | SPEC §2 |
| Compatibilidade de escrita com 1.x | **CONSOLIDADA**: ler tudo; escrever 2.x com proteção forward e backup (REQ-035, REQ-038, REQ-082) | ADR-0004 |
| Criptografia/armazenamento de chaves | **CONSOLIDADA (aprovada 2026-09-30)**: mitigações agora (REQ-053); cripto nativa ADIADA (REQ-054) | ADR-0007 |
| Metas numéricas de performance | **ADIADA com gate**: medir baseline primeiro (REQ-070) e fixar budgets (REQ-071) | PERFORMANCE |
| Identidade em sidecar vs frontmatter | **CONSOLIDADA (aprovada 2026-09-30)**: sidecar `.urbe/` (REQ-042) | ADR-0006 |

Detalhes, alternativas e perguntas objetivas: `docs/v2/discovery/OPEN-DECISIONS.md`.

## Histórico de mudanças do ledger

- Fundação (PR #34): REQ-001..024.
- Descoberta (issue #33): REQ-025..088 adicionados; nenhum REQ removido ou alterado em estado. Decisões abertas atualizadas para "status após a descoberta".

## Regra de preservação

Um requisito pode mudar de estado, mas nunca ser removido silenciosamente. Toda substituição, rejeição, adiamento ou saída de escopo deve registrar justificativa e requisito substituto quando aplicável.
