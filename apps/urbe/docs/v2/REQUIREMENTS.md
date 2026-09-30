# Requirement Ledger — Urbe 2.0

> Status: inicial, em descoberta.
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

## Decisões ainda abertas

Estas perguntas não devem ser respondidas por conveniência durante uma implementação:

- manter runtime sem build ou migrar para ES modules/bundler;
- modelo final de plugins;
- política de licença;
- mecanismo exato de release;
- estrutura física final de pastas da 2.0;
- nível de compatibilidade de escrita com versões 1.x;
- política de criptografia/armazenamento de chaves;
- metas numéricas de performance por classe de dispositivo/vault.

Quando uma decisão for tomada, ela deve atualizar este ledger e, se arquiteturalmente significativa, gerar ADR.

## Regra de preservação

Um requisito pode mudar de estado, mas nunca ser removido silenciosamente. Toda substituição, rejeição, adiamento ou saída de escopo deve registrar justificativa e requisito substituto quando aplicável.
