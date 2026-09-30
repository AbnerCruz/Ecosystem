# AGENTS.md — Instruções operacionais para agentes

> **Autoridade:** operacional, subordinada ao [`MANIFEST.md`](MANIFEST.md) (ver §24 do manifesto).
> Este arquivo **não substitui** o manifesto. Em caso de divergência, o `MANIFEST.md` prevalece e a divergência deve ser corrigida aqui.

---

## 1. Protocolo obrigatório de trabalho

Todo agente que trabalhe neste repositório deve, nesta ordem:

1. ler [`MANIFEST.md`](MANIFEST.md);
2. ler integralmente a seção `0.1 NON-NEGOTIABLES` do `MANIFEST.md` (a seção 2 abaixo é a reprodução operacional, não o original);
3. ler [`ecosystem.json`](ecosystem.json);
4. identificar o componente afetado pela tarefa;
5. ler os documentos normativos daquele componente (SPEC, ROADMAP, `AGENTS.md` local, ADRs e contracts relacionados);
6. verificar o estado atual do Git (`git status`, `git log`, branch atual);
7. verificar branches, PRs, tarefas e handoffs relevantes (`docs/governance/handoffs/`, [`docs/governance/decisions.json`](docs/governance/decisions.json)), incluindo decisões já tomadas e adendos do proprietário em [`docs/governance/addenda/`](docs/governance/addenda/) — eles estão no topo da hierarquia de autoridade;
8. declarar o escopo da tarefa;
9. identificar quais `NN-XXX` são relevantes para a tarefa;
10. respeitar boundaries ([`ARCHITECTURE.md`](ARCHITECTURE.md) §4);
11. implementar;
12. testar;
13. verificar — no mínimo `dotnet run tests/consistency/Check.cs` (ver §5);
14. documentar (documentação atualizada no mesmo conjunto de mudanças que o código);
15. registrar handoff/resultado em `docs/governance/handoffs/` (ver [`docs/governance/communication.md`](docs/governance/communication.md));
16. não declarar conclusão sem evidência ([`docs/governance/definition-of-done.md`](docs/governance/definition-of-done.md)).

Quando uma tarefa tocar **arquitetura, governança, migração, protocolo ou contrato**, o agente deve consultar diretamente a fonte normativa relevante — resumos (inclusive este arquivo) nunca bastam (NN-010, MANIFEST §52). O handoff de uma tarefa estrutural deve registrar os documentos normativos consultados e os `NN-XXX` considerados, com evidência de que foram respeitados.

---

## 2. NON-NEGOTIABLES (reprodução operacional de MANIFEST §0.1)

Estas invariantes são **obrigatórias**. Nenhuma pode ser removida, suavizada, ter seu escopo alterado ou ser tratada como recomendação. A matriz legível por máquina que associa cada invariante aos mecanismos de fiscalização é [`docs/governance/enforcement-matrix.json`](docs/governance/enforcement-matrix.json). A impossibilidade de automatizar uma regra **não** autoriza sua remoção.

Legenda de mecanismos (MANIFEST §0.2): `DOC` documentação normativa · `SCHEMA` validação de manifest/contract · `TEST` teste automatizado · `ARCH` teste de architecture/boundary · `CI` pipeline obrigatório · `REVIEW` revisão humana ou de agente independente · `ADR` decisão arquitetural formal · `DEVICE` validação em aparelho · `RUNTIME` observabilidade/verificação em execução.

### NN-001 — Uma única autoridade por conceito
Nenhum estado importante pode ter duas fontes canônicas concorrentes. Aplica-se, entre outros, a: versão, estado de tarefa, manifest, capability, contrato, roadmap, ownership de dados, estado de agente e configuração persistida. Caches, índices e projeções podem existir, mas **devem declarar sua fonte de verdade**.
**Enforcement obrigatório:** documentação de ownership; manifests; testes de consistência quando automatizáveis; o CI **deve falhar** quando duas autoridades declaradas forem detectáveis automaticamente.

### NN-002 — Monorepo não significa monólito
Urbe, Lunet2D e Hub permanecem produtos independentes. É **proibida** dependência direta `Urbe → Lunet2D` e `Lunet2D → Urbe`. Integração ocorre apenas por contracts/capabilities/adapters apropriados.
**Enforcement obrigatório:** architecture tests; dependency graph; CI de boundaries.

### NN-003 — O Hub jamais é dependência essencial dos produtos
O Hub é Control Plane, Host geral, Registry e Launcher; **não** é requisito para o funcionamento essencial do Lunet2D ou do Urbe. Com o Hub ausente, offline, desatualizado ou quebrado, os produtos continuam funcionando em seu domínio essencial.
**Enforcement obrigatório:** testes de execução standalone; ausência de dependência obrigatória do Hub no grafo arquitetural; gate de release dos produtos sem Hub.

### NN-004 — Não existe `shared` sem responsabilidade
É **proibido** criar área genérica para código "compartilhado" sem dono conceitual claro. Todo componente compartilhado deve declarar: responsabilidade, contrato, consumidores, owner, compatibilidade e motivo da extração.
**Enforcement obrigatório:** revisão arquitetural; manifesto de componentes; check de diretórios proibidos/genéricos, quando aplicável.

### NN-005 — C# é padrão, não justificativa para reescrita
Nova infraestrutura compartilhada usa C# por padrão. Isso **não** autoriza reescrita geral do Urbe. Migração de código existente exige benefício demonstrável, estratégia incremental, testes e preservação de comportamento/dados.
**Enforcement obrigatório:** qualquer reescrita estrutural exige ADR; roadmap específico; testes de compatibilidade; aprovação explícita quando houver mudança relevante de produto/dados.

### NN-006 — Capability é contrato, não integração improvisada
Toda capability pública ou compartilhada deve ter contrato explícito com, no mínimo: ID estável, versão, provider, inputs, outputs, erros, permissões, lifecycle e política de compatibilidade. Integrações especiais "só para este caso" são dívida arquitetural e devem ser justificadas.
**Enforcement obrigatório:** schema/contract validável; contract tests; compatibility checks.

### NN-007 — Uma Tool não conhece o Host concreto
Ferramentas compartilháveis **não podem** depender de nomes ou implementações concretas de Hosts. É proibida lógica equivalente a `if host == Lunet` / `if host == Hub` / `if host == Urbe` quando a diferença pode ser expressa por capability/context/contract.
**Enforcement obrigatório:** dependency tests; revisão de imports/references; contract tests em pelo menos dois Hosts quando a Tool for declarada multi-host.

### NN-008 — Nenhum agente realiza trabalho invisível
Toda execução relevante de agente deve ser observável. Deve ser possível descobrir, conforme o estágio permitir: Agent ID, projeto/componente, task ID, estado, branch, commit, PR, arquivos impactados, trabalho concluído, verificações, bloqueios, próxima ação declarada, decisões necessárias e custo, quando aplicável.
**Enforcement obrigatório:** schema de task/handoff; validação automática dos registros; tarefas **não podem** ser consideradas concluídas sem handoff/resultado verificável.

### NN-009 — Conversa não é fonte de verdade permanente
Decisão material tomada em chat, reunião ou interação com IA **deve** ser persistida em fonte canônica antes de sustentar implementação duradoura. "Foi decidido em uma conversa" não é documentação suficiente.
**Enforcement obrigatório:** decisão estrutural → ADR ou documento normativo equivalente; decisão de produto → SPEC/requirements/roadmap conforme aplicável; handoff deve apontar para a fonte persistida.

### NN-010 — Resumo nunca substitui documento normativo
Resumos (incluindo este `AGENTS.md`) servem para navegação e contexto rápido; **nunca** substituem `MANIFEST.md`, contracts, SPECs, ADRs ou outras fontes normativas. Quando a tarefa toca arquitetura, governança, migração, protocolo ou contrato, o agente **deve** consultar diretamente a fonte normativa relevante.
**Enforcement obrigatório:** `AGENTS.md` exige essa leitura (§1 acima); handoff de tarefa estrutural deve registrar quais documentos normativos foram consultados.

### NN-011 — Mudança arquitetural exige decisão explícita
Agentes podem decidir autonomamente detalhes locais de implementação. Agentes **não podem** alterar silenciosamente: boundaries, protocolo, ownership, modelo de dados, compatibilidade pública, responsabilidade de componente, lifecycle de produto ou decisão consolidada. Essas mudanças exigem ADR e, quando afetarem direção do produto ou risco relevante, decisão do proprietário.
**Enforcement obrigatório:** ADR check; review gate; traceability entre ADR e mudança.

### NN-012 — Histórico Git deve ser preservado nas migrações
Migrar Urbe, Lunet2D ou outro projeto para o monorepo **não** autoriza descartar histórico relevante. Copiar apenas o snapshot atual é insuficiente.
**Enforcement obrigatório:** plano de migração documentado; verificação de commits/tags relevantes; auditoria pós-migração.

### NN-013 — Migração e refatoração não são a mesma tarefa
Mover um projeto para o monorepo deve preservar comportamento. Refatoração, reorganização funcional, limpeza e mudanças de arquitetura ocorrem em itens separados, salvo necessidade técnica inevitável explicitamente documentada.
**Enforcement obrigatório:** PRs/commits de migração com escopo restrito; auditoria de diff; testes antes/depois.

### NN-014 — Produtos possuem lifecycles e versões independentes
O monorepo tem histórico comum. Urbe, Lunet2D, Hub e componentes públicos **não** são obrigados a compartilhar versão ou release. Mudança em um produto não cria artificialmente release de outro.
**Enforcement obrigatório:** manifests de produto; pipelines seletivos; versionamento independente.

### NN-015 — GitHub não é IPC
GitHub é registro de desenvolvimento, distribuição e colaboração. **Não** deve ser usado como mecanismo primário de comunicação runtime entre aplicativos.
**Enforcement obrigatório:** runtime integration deve utilizar contratos/transportes próprios; qualquer exceção exige ADR.

### NN-016 — Permissões precedem autonomia forte
Agentes, plugins, tools e services **não** recebem acesso irrestrito por conveniência. Acesso a filesystem, projeto, Git, GitHub, build, network, assets, execução e outras operações sensíveis deve ser explicitamente modelado.
**Enforcement obrigatório:** permission model; permission declaration nos manifests; deny-by-default quando aplicável.

### NN-017 — CI verde não equivale a validação completa
Testes automáticos são necessários, mas **não** substituem validação humana quando o requisito depende de: dispositivo real, Android lifecycle, toque, layout, performance percebida, comportamento visual ou instalação/atualização.
**Enforcement obrigatório:** gates distinguem `automated verified` de `human validated`; o roadmap **não** marca como concluído o que ainda depende de validação humana.

### NN-018 — “Done” exige evidência
Uma tarefa não está concluída porque um agente diz que terminou ou porque arquivos foram alterados. Quando aplicável, conclusão exige: implementação, integração, testes, verificação, documentação, compatibilidade, CI, evidência e validação humana, quando necessária.
**Enforcement obrigatório:** Definition of Done; status machine; links/evidências no handoff.

### NN-019 — Identidade importante deve ser estável
Projetos, capabilities, contracts, tasks, agent messages e outros elementos de longa duração **devem** ter IDs estáveis. Nomes de exibição podem mudar; identidade não depende apenas do nome.
**Enforcement obrigatório:** schemas com IDs; validação de unicidade; migração explícita quando um ID realmente precisar mudar.

### NN-020 — A plataforma deve reduzir, não aumentar, a complexidade total
Nenhuma abstração compartilhada merece existir apenas por parecer elegante. Um novo runtime, package, SDK, service ou framework compartilhado **deve demonstrar** que reduz duplicação, acoplamento ou custo de evolução. Se a plataforma bloquear o avanço de Urbe e Lunet2D sem benefício proporcional, a abstração deve ser reavaliada.
**Enforcement obrigatório:** critério de criação de componente (MANIFEST §48); ADR para abstrações estruturais; revisão periódica de dívida e custo de integração.

### NN-021 — O estado real do ecossistema nunca pode ficar implícito
Se algo existe, deve ser descobrível. Se depende de outro componente, deve ser declarado. Se mudou, deve existir histórico. Se quebrou, deve aparecer. Se um agente está trabalhando, deve ser observável. Se depende do proprietário, deve aparecer como decisão pendente. Se foi concluído, deve possuir evidência.
**Enforcement obrigatório:** manifests; status model; observability; traceability; o Hub deve consumir essas fontes em vez de inventar estado.

### NN-022 — Nenhuma abstração compartilhada sem redução de complexidade total
Antes de extrair algo para `platform/`, `workspaces/` ou `tools/`, deve ser respondido: (1) qual complexidade é removida; (2) quais consumidores reais existem; (3) qual contrato estabiliza a relação; (4) qual custo novo de versionamento/integração surge; (5) por que o saldo final é positivo. Se isso não puder ser demonstrado, o componente permanece onde está.
**Enforcement obrigatório:** ADR ou registro arquitetural para extrações relevantes; pelo menos um teste de consumidor real; revisão de boundary.

### NN-023 — Produtos distribuíveis não dependem da distribuição do Hub
Um Product declarado distribuível deve poder ser empacotado, publicado, instalado, atualizado e utilizado em seu domínio essencial **sem exigir que o Ecosystem Hub seja distribuído ao usuário final**. Complementa NN-003 e não a substitui: NN-003 trata do *funcionamento*, NN-023 da *distribuição*. O Hub pode permanecer privado/interno e nunca ser publicado; isso não pode impedir a distribuição completa de nenhum Product.
**Enforcement obrigatório:** dependency graph; packaging; release pipeline; testes standalone; validação de Distribution Profile; CI; DEVICE, quando aplicável.

---

## 3. Autonomia e decisões

- **Pode decidir sozinho** (MANIFEST §22.4): detalhes de implementação dentro de contratos aprovados que não mudem produto, boundary, compatibilidade, dados, decisão consolidada nem adicionem tecnologia estrutural sem necessidade.
- **Não pode decidir sozinho:** tudo listado em NN-011. Registre um ADR com status `Proposto` e uma entrada `pending` em [`docs/governance/decisions.json`](docs/governance/decisions.json), no formato de MANIFEST §22.3.
- **Nunca** transforme preferência própria em decisão do proprietário, nem inferência em requisito (MANIFEST §53). Separe `FATO OBSERVADO`, `INFERÊNCIA`, `PROPOSTA` e `DECISÃO`.
- **Toda decisão pendente e toda validação humana pendente devem aparecer no portal, com o objeto** a decidir/validar (DEC-0007): entrada `pending` em `decisions.json` com `related`; verificação humana pendente em handoff com `object`. Nunca peça decisão só no chat. Regras em [`docs/governance/communication.md`](docs/governance/communication.md) §9.
- **Estado vivo das tarefas** fica em Issues do GitHub; o ROADMAP é a autoridade de escopo e IDs; handoffs registram resultado e evidência (DEC-0003, [`communication.md`](docs/governance/communication.md) §8).
- **Arquitetura não é distribuição; Product Shell não é Hub; Connections é UX, não arquitetura paralela.** Conceitos de Product Shell, Context, Distribution Profile, Store × Library e estratégia first-party: [`docs/architecture/product-model.md`](docs/architecture/product-model.md) e [`docs/architecture/distribution.md`](docs/architecture/distribution.md) (ADD-0002). São conceitos registrados, **não** autorização para criar código, diretórios, Services ou abstrações (NN-020, NN-022).
- **Portal web (`site/`)** é projeção, nunca fonte de verdade: não escreva nele dados que tenham autoridade em outro lugar (ADD-0001, [`docs/architecture/portal.md`](docs/architecture/portal.md)).
- **Hierarquia de autoridade** (MANIFEST §24): decisão registrada do proprietário > `MANIFEST.md` > ADRs aprovados > `ARCHITECTURE.md` e contracts > SPEC > ROADMAP > tarefa/Issue > handoffs > comentários informais > inferências.

## 4. Comunicação e handoff

Regras completas: [`docs/governance/communication.md`](docs/governance/communication.md). Em resumo:

- Um handoff é um arquivo JSON em `docs/governance/handoffs/`, validado por [`docs/contracts/schemas/handoff.schema.json`](docs/contracts/schemas/handoff.schema.json).
- Um agente nunca escreve como outro agente e nunca inventa revisão, aprovação ou decisão (MANIFEST §23.2).
- Não sobrescreva trabalho de outro agente para resolver divergência (MANIFEST §23.3).
- Segredos, tokens e chaves nunca entram em commits, logs ou handoffs (MANIFEST §30.2).

## 5. Verificação local

```bash
dotnet run tests/consistency/Check.cs
```

Requer .NET SDK 10+. O mesmo check roda no CI ([`.github/workflows/consistency.yml`](.github/workflows/consistency.yml)). Detalhes: [`tests/consistency/README.md`](tests/consistency/README.md).

## 6. Instruções locais

Componentes podem ter `AGENTS.md` próprio (ex.: `apps/lunet2d/AGENTS.md`). A regra local complementa esta; **não pode contradizê-la** (MANIFEST §26).
