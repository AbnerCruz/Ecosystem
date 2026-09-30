# MANIFEST.md — Ecosystem

> **Status:** Constituição inicial do produto  
> **Autoridade:** normativa  
> **Idioma normativo:** português  
> **Objetivo:** definir o produto, os princípios, os limites arquiteturais, a governança e os protocolos que permitem que humanos e agentes desenvolvam o ecossistema sem perder coerência ao longo do tempo.

---

## 0. Natureza deste documento

Este arquivo não é um README promocional, um brainstorm nem um resumo de arquitetura.

Ele é a **constituição do Ecosystem**.

Toda implementação, especificação, roadmap, ADR, agente, workspace, aplicativo, capability, biblioteca ou automação criada dentro deste repositório deve ser compatível com os princípios aqui definidos.

Este manifesto pode evoluir, mas **não pode ser silenciosamente reinterpretado, resumido, enfraquecido ou substituído**.

Uma mudança que contradiga este documento exige:

1. identificação explícita da contradição;
2. justificativa;
3. análise de impacto no ecossistema;
4. decisão registrada do proprietário;
5. atualização deste manifesto e dos documentos derivados no mesmo conjunto de mudanças.

Um resumo deste arquivo pode existir para navegação, mas **nunca substitui a leitura integral do `MANIFEST.md` por um agente que esteja tomando decisões estruturais**.

Nenhum agente pode alegar que "seguiu a intenção geral" enquanto viola uma cláusula explícita deste documento.

---

# 0.1 NON-NEGOTIABLES — Invariantes que jamais podem ser violadas

Esta seção concentra as regras cuja violação pode comprometer a integridade do Ecosystem.

Ela existe deliberadamente de forma redundante em relação ao restante do manifesto. Essa redundância é uma proteção operacional contra leitura parcial, compressão de contexto, handoff incompleto e interpretação oportunista.

Todo `AGENTS.md` raiz deve reproduzir estas invariantes de forma operacional, preservando seu significado. Um resumo pode ser mais curto, mas não pode eliminar, suavizar ou reinterpretar nenhuma delas.

Cada invariante possui um identificador estável `NN-XXX`.

## NN-001 — Uma única autoridade por conceito

Nenhum estado importante pode possuir duas fontes canônicas concorrentes.

Aplica-se, entre outros, a:

- versão;
- estado de tarefa;
- manifest;
- capability;
- contrato;
- roadmap;
- ownership de dados;
- estado de agente;
- configuração persistida.

Caches, índices e projeções podem existir, mas precisam declarar sua fonte de verdade.

**Enforcement obrigatório:**
- documentação de ownership;
- manifests;
- testes de consistência quando automatizáveis;
- CI deve falhar quando duas autoridades declaradas forem detectáveis automaticamente.

---

## NN-002 — Monorepo não significa monólito

Urbe, Lunet2D e Hub permanecem produtos independentes.

É proibida dependência direta:

```text
Urbe → Lunet2D
Lunet2D → Urbe
```

Integração deve ocorrer por contracts/capabilities/adapters apropriados.

**Enforcement obrigatório:**
- architecture tests;
- dependency graph;
- CI de boundaries.

---

## NN-003 — O Hub jamais é dependência essencial dos produtos

O Hub é Control Plane, Host geral, Registry e Launcher.

Ele não é requisito para o funcionamento essencial do Lunet2D ou do Urbe.

Se o Hub estiver ausente, offline, desatualizado ou quebrado, os produtos continuam funcionando em seu domínio essencial.

**Enforcement obrigatório:**
- testes de execução standalone;
- ausência de dependência obrigatória do Hub no grafo arquitetural;
- gate de release dos produtos sem Hub.

---

## NN-004 — Não existe `shared` sem responsabilidade

É proibido criar uma área genérica para código “compartilhado” sem dono conceitual claro.

Todo componente compartilhado precisa declarar:

- responsabilidade;
- contrato;
- consumidores;
- owner;
- compatibilidade;
- motivo da extração.

**Enforcement obrigatório:**
- revisão arquitetural;
- manifesto de componentes;
- check de diretórios proibidos/genéricos, quando aplicável.

---

## NN-005 — C# é padrão, não justificativa para reescrita

Nova infraestrutura compartilhada usa C# por padrão.

Isso não autoriza reescrita geral do Urbe.

Migração de código existente exige benefício demonstrável, estratégia incremental, testes e preservação de comportamento/dados.

**Enforcement obrigatório:**
- qualquer reescrita estrutural exige ADR;
- roadmap específico;
- testes de compatibilidade;
- aprovação explícita quando houver mudança relevante de produto/dados.

---

## NN-006 — Capability é contrato, não integração improvisada

Toda capability pública ou compartilhada deve possuir contrato explícito.

No mínimo:

- ID estável;
- versão;
- provider;
- inputs;
- outputs;
- erros;
- permissões;
- lifecycle;
- política de compatibilidade.

Integrações especiais “só para este caso” são sinal de dívida arquitetural e devem ser justificadas.

**Enforcement obrigatório:**
- schema/contract validável;
- contract tests;
- compatibility checks.

---

## NN-007 — Uma Tool não conhece o Host concreto

Ferramentas compartilháveis não podem depender de nomes ou implementações concretas de Hosts.

É proibido criar lógica equivalente a:

```text
if host == Lunet
if host == Hub
if host == Urbe
```

quando a diferença pode ser expressa por capability/context/contract.

**Enforcement obrigatório:**
- dependency tests;
- revisão de imports/references;
- contract tests em pelo menos dois Hosts quando a Tool for declarada multi-host.

---

## NN-008 — Nenhum agente realiza trabalho invisível

Toda execução relevante de agente deve ser observável.

Deve ser possível descobrir, conforme o estágio permitir:

- Agent ID;
- projeto/componente;
- task ID;
- estado;
- branch;
- commit;
- PR;
- arquivos impactados;
- trabalho concluído;
- verificações;
- bloqueios;
- próxima ação declarada;
- decisões necessárias;
- custo, quando aplicável.

**Enforcement obrigatório:**
- schema de task/handoff;
- validação automática dos registros;
- tarefas não podem ser consideradas concluídas sem handoff/resultado verificável.

---

## NN-009 — Conversa não é fonte de verdade permanente

Decisão material tomada em chat, reunião ou interação com IA precisa ser persistida em fonte canônica antes de sustentar implementação duradoura.

“Foi decidido em uma conversa” não é documentação suficiente.

**Enforcement obrigatório:**
- decisão estrutural → ADR ou documento normativo equivalente;
- decisão de produto → SPEC/requirements/roadmap conforme aplicável;
- handoff deve apontar para a fonte persistida.

---

## NN-010 — Resumo nunca substitui documento normativo

Resumos são permitidos para navegação e contexto rápido.

Eles nunca substituem `MANIFEST.md`, contracts, SPECs, ADRs ou outras fontes normativas.

Quando uma tarefa toca arquitetura, governança, migração, protocolo ou contrato, o agente deve consultar diretamente a fonte normativa relevante.

**Enforcement obrigatório:**
- `AGENTS.md` deve exigir essa leitura;
- handoff de tarefa estrutural deve registrar quais documentos normativos foram consultados.

---

## NN-011 — Mudança arquitetural exige decisão explícita

Agentes podem decidir autonomamente detalhes locais de implementação.

Agentes não podem alterar silenciosamente:

- boundaries;
- protocolo;
- ownership;
- modelo de dados;
- compatibilidade pública;
- responsabilidade de componente;
- lifecycle de produto;
- decisão consolidada.

Mudanças desse tipo exigem ADR e, quando afetarem direção do produto ou risco relevante, decisão do proprietário.

**Enforcement obrigatório:**
- ADR check;
- review gate;
- traceability entre ADR e mudança.

---

## NN-012 — Histórico Git deve ser preservado nas migrações

Migrar Urbe, Lunet2D ou outro projeto para o monorepo não autoriza descartar histórico relevante.

Copiar apenas o snapshot atual é insuficiente.

**Enforcement obrigatório:**
- plano de migração documentado;
- verificação de commits/tags relevantes;
- auditoria pós-migração.

---

## NN-013 — Migração e refatoração não são a mesma tarefa

Mover um projeto para o monorepo deve preservar comportamento.

Refatoração, reorganização funcional, limpeza e mudanças de arquitetura devem ocorrer em itens separados, salvo necessidade técnica inevitável explicitamente documentada.

**Enforcement obrigatório:**
- PRs/commits de migração com escopo restrito;
- auditoria de diff;
- testes antes/depois.

---

## NN-014 — Produtos possuem lifecycles e versões independentes

O monorepo possui histórico comum.

Urbe, Lunet2D, Hub e componentes públicos não são obrigados a compartilhar versão ou release.

Mudança em um produto não cria artificialmente release de outro.

**Enforcement obrigatório:**
- manifests de produto;
- pipelines seletivos;
- versionamento independente.

---

## NN-015 — GitHub não é IPC

GitHub é registro de desenvolvimento, distribuição e colaboração.

Não deve ser usado como mecanismo primário de comunicação runtime entre aplicativos.

**Enforcement obrigatório:**
- runtime integration deve utilizar contratos/transportes próprios;
- qualquer exceção exige ADR.

---

## NN-016 — Permissões precedem autonomia forte

Agentes, plugins, tools e services não recebem acesso irrestrito por conveniência.

Acesso a filesystem, projeto, Git, GitHub, build, network, assets, execução e outras operações sensíveis deve ser explicitamente modelado.

**Enforcement obrigatório:**
- permission model;
- permission declaration nos manifests;
- deny-by-default quando aplicável.

---

## NN-017 — CI verde não equivale a validação completa

Testes automáticos são necessários, mas não substituem validação humana quando o requisito depende de:

- dispositivo real;
- Android lifecycle;
- toque;
- layout;
- performance percebida;
- comportamento visual;
- instalação/atualização.

**Enforcement obrigatório:**
- gates distinguem `automated verified` de `human validated`;
- roadmap não marca como concluído o que ainda depende de validação humana.

---

## NN-018 — “Done” exige evidência

Uma tarefa não está concluída porque um agente diz que terminou ou porque arquivos foram alterados.

Quando aplicável, conclusão exige:

- implementação;
- integração;
- testes;
- verificação;
- documentação;
- compatibilidade;
- CI;
- evidência;
- validação humana, quando necessária.

**Enforcement obrigatório:**
- Definition of Done;
- status machine;
- links/evidências no handoff.

---

## NN-019 — Identidade importante deve ser estável

Projetos, capabilities, contracts, tasks, agent messages e outros elementos de longa duração devem possuir IDs estáveis.

Nomes de exibição podem mudar.

Identidade não deve depender apenas do nome.

**Enforcement obrigatório:**
- schemas com IDs;
- validação de unicidade;
- migração explícita quando um ID realmente precisar mudar.

---

## NN-020 — A plataforma deve reduzir, não aumentar, a complexidade total

Nenhuma abstração compartilhada merece existir apenas porque parece arquiteturalmente elegante.

Um novo runtime, package, SDK, service ou framework compartilhado precisa demonstrar que reduz duplicação, acoplamento ou custo de evolução.

Se a plataforma começar a bloquear o avanço de Urbe e Lunet2D sem benefício proporcional, a abstração deve ser reavaliada.

**Enforcement obrigatório:**
- critério de criação de componente;
- ADR para abstrações estruturais;
- revisão periódica de dívida e custo de integração.

---

## NN-021 — O estado real do ecossistema nunca pode ficar implícito

Se algo existe, deve ser descobrível.

Se algo depende de outro componente, deve ser declarado.

Se mudou, deve existir histórico.

Se quebrou, deve aparecer.

Se um agente está trabalhando, deve ser observável.

Se depende do proprietário, deve aparecer como decisão pendente.

Se foi concluído, deve possuir evidência.

**Enforcement obrigatório:**
- manifests;
- status model;
- observability;
- traceability;
- Hub deve consumir essas fontes em vez de inventar estado.

---

## NN-022 — Nenhuma abstração compartilhada sem redução de complexidade total

Antes de extrair algo para `platform/`, `workspaces/` ou `tools/`, deve ser respondido:

1. qual complexidade é removida;
2. quais consumidores reais existem;
3. qual contrato estabiliza a relação;
4. qual custo novo de versionamento/integracão surge;
5. por que o saldo final é positivo.

Se isso não puder ser demonstrado, o componente permanece onde está.

**Enforcement obrigatório:**
- ADR ou registro arquitetural para extrações relevantes;
- pelo menos um teste de consumidor real;
- revisão de boundary.

---

## 0.2 Matriz de enforcement

Toda regra crítica deve ser classificada por uma ou mais formas de fiscalização:

```text
DOC      = documentação normativa
SCHEMA   = validação de manifest/contract
TEST     = teste automatizado
ARCH     = architecture/boundary test
CI       = pipeline obrigatório
REVIEW   = revisão humana ou de agente independente
ADR      = decisão arquitetural formal
DEVICE   = validação em aparelho
RUNTIME  = observabilidade/verificação em execução
```

O repositório deve manter uma matriz legível por máquina, ou equivalente, que associe `NN-XXX` aos mecanismos aplicáveis.

Exemplo conceitual:

```text
NN-001 → DOC, SCHEMA, TEST, CI
NN-002 → ARCH, TEST, CI
NN-003 → ARCH, TEST, DEVICE
NN-008 → SCHEMA, CI, RUNTIME
NN-011 → ADR, REVIEW
NN-017 → CI, DEVICE
NN-018 → SCHEMA, CI, REVIEW, DEVICE
```

Uma regra que ainda não possa ser automatizada deve continuar existindo como regra. A impossibilidade de enforcement automático não autoriza sua remoção.

---


# 1. Visão

O Ecosystem é uma plataforma modular para desenvolver, executar, observar, combinar e evoluir aplicativos, ferramentas, workspaces, agentes e serviços sob uma arquitetura comum.

Os dois produtos principais iniciais são:

- **Lunet2D** — ambiente de desenvolvimento de jogos 2D, framework, runtime e tooling;
- **Urbe** — workspace de conhecimento, documentos, criação e organização.

Eles permanecem produtos independentes.

O Ecosystem nasce para resolver um problema que cresce junto com esses produtos:

> Quanto mais projetos, ferramentas e agentes trabalham de forma autônoma, mais difícil se torna para o proprietário saber com precisão o que existe, o que aconteceu, o que está acontecendo, o que depende de quê, quem está modificando o quê e o que acontecerá em seguida.

O Ecosystem deve transformar essa complexidade em uma estrutura observável, inspecionável e controlável.

O usuário deve poder abrir um único aplicativo e compreender o estado do ecossistema inteiro.

Ao mesmo tempo, cada produto deve continuar podendo funcionar sem depender desse aplicativo central.

---

# 2. Produto central: um workspace de workspaces

O Ecosystem não é apenas um launcher.

Também não é apenas um monorepo.

E não deve se tornar um "superaplicativo" monolítico que absorve todos os outros.

O produto é composto por quatro conceitos principais:

1. **Produtos**
2. **Hosts**
3. **Capabilities**
4. **Control Plane**

A combinação deles permite que uma mesma ferramenta seja usada em contextos diferentes sem ser duplicada.

Exemplo fundamental:

- o **Sprite Studio** pode ser aberto diretamente no Hub;
- o mesmo Sprite Studio pode ser aberto dentro do Lunet2D;
- um agente pode utilizar operações do Sprite Studio;
- outro produto futuro pode utilizar o mesmo Sprite Studio;
- o Sprite Studio não precisa saber quem o chamou.

Essa propriedade é um requisito arquitetural do Ecosystem.

---

# 3. Princípio fundamental: produtos não possuem ferramentas compartilháveis

Uma funcionalidade reutilizável não deve ser implementada como propriedade privada de um produto quando ela possui valor independente.

Portanto, conceitualmente:

```text
ERRADO

Lunet2D
└── SpriteEditor

Hub
└── OutroSpriteEditor
```

O modelo desejado é:

```text
Sprite Studio
├── Core
├── Contracts
├── UI
└── Integration

Pode ser hospedado por:
├── Hub
├── Lunet2D
└── outros Hosts compatíveis
```

O mesmo vale, quando aplicável, para:

- Editor de código;
- Agent Workspace;
- GitHub Workspace;
- Sprite Studio;
- Tilemap Studio;
- Atlas Studio;
- Audio Studio;
- Font Studio;
- Particle Studio;
- Profiler;
- Documentation Workspace;
- Asset Browser;
- ferramentas futuras.

Nem toda funcionalidade deve ser extraída. A extração só é válida quando existe um domínio suficientemente independente e um contrato coerente.

---

# 4. Linguagem padrão

## 4.1 Regra

**C# é a linguagem padrão para toda nova infraestrutura compartilhada do Ecosystem.**

Isto inclui, salvo decisão arquitetural explícita em contrário:

- novos aplicativos;
- novos Hosts;
- novos runtimes;
- novos serviços;
- novos workspaces;
- novos protocolos;
- novas ferramentas compartilhadas;
- bibliotecas comuns;
- SDKs;
- contracts;
- IPC;
- capability registry;
- Hub.

## 4.2 Urbe

O Urbe existente não deve ser reescrito para C# apenas para satisfazer esta regra.

O Urbe possui código, dados, comportamento e usuários potenciais que precisam ser preservados.

Portanto:

- o código JavaScript existente permanece válido;
- migrações devem ser incrementais;
- novas integrações com o Ecosystem podem utilizar adapters;
- uma migração de partes do Urbe para C# só deve ocorrer quando houver benefício arquitetural e funcional demonstrável;
- reescrita geral do Urbe não é consequência automática da criação deste repositório.

## 4.3 Protocolos

Os contratos do ecossistema devem ser semanticamente independentes da linguagem sempre que precisarem atravessar processos, aplicações ou runtimes.

C# é a implementação padrão, não desculpa para criar protocolos impossíveis de consumir por componentes não-C#.

---

# 5. Os três produtos principais iniciais

## 5.1 Lunet2D

O Lunet2D continua sendo um produto especializado em desenvolvimento de jogos 2D.

Seu núcleo deve permanecer utilizável sozinho.

Uma instalação mínima do Lunet deve fornecer as capacidades essenciais do produto, incluindo seu domínio fundamental de projeto, C#, compilação, runtime, preview e framework conforme sua própria especificação.

Ferramentas avançadas podem ser capabilities conectáveis.

Exemplo:

```text
Lunet2D

Core
✓ projetos
✓ C#
✓ compilação
✓ runtime
✓ preview
✓ framework

Capabilities opcionais
○ Sprite Studio
○ Agent Workspace
○ Tilemap Studio
○ Audio Studio
○ GitHub Workspace
```

O Lunet não deve conter duplicatas privadas dessas capabilities apenas para evitar a integração correta.

## 5.2 Urbe

O Urbe continua sendo um produto especializado em conhecimento, documentos, vault, páginas, mundo e organização.

Seu domínio é próprio.

O Urbe pode expor capabilities e consumir capabilities do Ecosystem.

Exemplos possíveis de capabilities fornecidas pelo Urbe:

```text
vault.read
vault.write
document.open
document.search
knowledge.search
page.render
```

Exemplos possíveis de capabilities consumidas:

```text
ecosystem.agent
ecosystem.git
ecosystem.editor
image.edit
```

O Urbe nunca deve depender diretamente do Lunet2D.

## 5.3 Ecosystem Hub

O Hub é o **Control Plane e Host geral** do ecossistema.

Ele deve permitir, progressivamente:

- descobrir produtos;
- inspecionar produtos;
- abrir produtos;
- verificar versões;
- baixar releases;
- atualizar produtos;
- iniciar produtos;
- abrir workspaces;
- abrir ferramentas standalone;
- visualizar capabilities instaladas;
- visualizar dependências;
- visualizar agentes;
- visualizar tarefas;
- visualizar CI;
- visualizar releases;
- visualizar decisões pendentes;
- reconstruir a timeline de desenvolvimento;
- responder "o que aconteceu, o que está acontecendo e o que vem depois".

O Hub deve ser extremamente útil.

O Hub **não deve ser um ponto único de falha**.

Se o Hub estiver ausente ou quebrado, Lunet2D e Urbe devem continuar capazes de executar suas funções essenciais.

---

# 6. Definições normativas

## 6.1 Product

Aplicativo ou sistema com domínio próprio e valor independente.

Exemplos:

- Lunet2D
- Urbe
- Ecosystem Hub

## 6.2 Host

Ambiente capaz de hospedar uma interface ou capability.

Exemplos:

- Hub
- Lunet2D
- futuramente outro produto compatível

Um Host oferece contexto e serviços. Ele não deve exigir que a ferramenta conheça a implementação concreta do Host.

## 6.3 Capability

Contrato versionado que representa uma capacidade fornecida por algum componente.

Exemplos:

```text
sprite.edit
asset.open
asset.write
code.edit
project.build
agent.run
git.status
github.inspect
document.search
```

Uma capability possui, no mínimo:

- ID estável;
- versão;
- provider;
- contrato;
- permissões;
- inputs;
- outputs;
- política de compatibilidade.

## 6.4 Tool

Capability com uma operação de usuário suficientemente independente para possuir interface própria.

Exemplos:

- Sprite Studio;
- Editor;
- Tilemap Studio.

Uma Tool deve poder, quando seu domínio permitir, ser aberta standalone.

## 6.5 Service

Capability primariamente operacional, que não depende de interface própria.

Exemplos:

- Agent Runtime;
- Git;
- Compiler;
- Build;
- Update service.

## 6.6 Library

Componente de código usado internamente e sem lifecycle independente de usuário.

Exemplos:

- matemática;
- serialização;
- estruturas de dados;
- contracts.

Uma Library não deve ser apresentada artificialmente como Tool ou Workspace.

## 6.7 Workspace

Superfície de trabalho que organiza contexto, ferramentas e estado para uma atividade.

Exemplos:

- Agent Workspace;
- GitHub Workspace;
- Editor Workspace;
- Documentation Workspace.

## 6.8 Adapter

Componente que traduz dois contratos incompatíveis ou conecta um sistema legado ao contrato do Ecosystem.

Adapters devem possuir propósito e condição de remoção quando temporários.

## 6.9 Contract

Descrição explícita, estável e versionada de uma interação.

Um contrato não é "o jeito como o código atualmente funciona".

Ele deve ser documentado e testável.

---

# 7. Regra de independência

Todo produto principal deve funcionar sozinho em seu domínio essencial.

Portanto:

```text
Hub ausente
→ Lunet funciona.

Hub ausente
→ Urbe funciona.

Agent Workspace ausente
→ Lunet funciona sem IA.

Sprite Studio ausente
→ Lunet continua funcional, apenas sem aquela capability avançada.
```

A experiência pode ficar mais poderosa quando componentes são conectados, mas uma dependência opcional nunca pode ser secretamente transformada em dependência essencial.

---

# 8. Capability-first architecture

Produtos e ferramentas não devem se conhecer por nomes concretos quando um contrato resolve a interação.

Exemplo proibido:

```csharp
if (host == "Lunet")
{
    ...
}
else if (host == "Hub")
{
    ...
}
```

Modelo esperado:

```text
Sprite Studio

requires:
  file.read
  file.write

optional:
  project.assets
  agent.tools
```

No Lunet:

```text
file.read      → Lunet Project Workspace
file.write     → Lunet Project Workspace
project.assets → Lunet Asset Service
```

No Hub:

```text
file.read      → Hub Files
file.write     → Hub Files
```

A ferramenta continua sendo a mesma.

---

# 9. Capability Registry

O Ecosystem deve possuir um registro capaz de responder:

- quais capabilities existem;
- quais providers estão disponíveis;
- quais versões estão instaladas;
- quais consumidores dependem delas;
- quais permissões exigem;
- quais versões são compatíveis;
- qual provider será usado para uma requisição.

O Registry não deve criar acoplamento entre aplicações.

Ele resolve descoberta.

Ele não transforma uma capability opcional em dependência obrigatória.

---

# 10. App Manifest e Component Manifest

O ecossistema precisa ser legível por humanos e máquinas.

O repositório raiz deve possuir um manifesto canônico, inicialmente chamado:

```text
ecosystem.json
```

Ele deve mapear todos os componentes relevantes.

Exemplo conceitual:

```json
{
  "projects": {
    "lunet2d": {
      "path": "apps/lunet2d",
      "type": "product",
      "language": "csharp"
    },
    "urbe": {
      "path": "apps/urbe",
      "type": "product",
      "language": "javascript"
    },
    "hub": {
      "path": "apps/hub",
      "type": "product",
      "language": "csharp"
    }
  }
}
```

Com a evolução do projeto, o manifesto deve poder declarar:

- ID;
- nome;
- path;
- tipo;
- linguagem;
- versão;
- arquivo de versão;
- comandos de build;
- comandos de teste;
- documentação;
- SPEC;
- ROADMAP;
- release;
- capabilities fornecidas;
- capabilities obrigatórias;
- capabilities opcionais;
- dependências;
- owners;
- plataformas;
- status.

Cada componente reutilizável deve possuir seu próprio manifest quando necessário.

O Hub e os agentes devem utilizar esses manifests como mapa da realidade.

Não devem inferir toda a arquitetura por grep indiscriminado.

---

# 11. Monorepo

Urbe e Lunet2D serão migrados progressivamente para um único monorepo.

O objetivo do monorepo é:

- permitir visão completa do ecossistema;
- permitir mudanças atômicas entre contratos e consumidores;
- facilitar auditoria;
- facilitar trabalho de agentes;
- permitir CI transversal;
- preservar coerência arquitetural;
- permitir ao Hub compreender o desenvolvimento inteiro através do GitHub.

O monorepo não transforma os produtos em um único programa.

## 11.1 Estrutura alvo inicial

Estrutura conceitual:

```text
/
├── MANIFEST.md
├── README.md
├── AGENTS.md
├── ARCHITECTURE.md
├── ROADMAP.md
├── ecosystem.json
│
├── apps/
│   ├── lunet2d/
│   ├── urbe/
│   └── hub/
│
├── platform/
│   ├── contracts/
│   ├── registry/
│   ├── runtime/
│   ├── ipc/
│   └── packaging/
│
├── workspaces/
│   ├── agent/
│   ├── editor/
│   ├── github/
│   └── documentation/
│
├── tools/
│   ├── sprite-studio/
│   └── ...
│
├── docs/
│   ├── architecture/
│   ├── contracts/
│   ├── adr/
│   ├── governance/
│   └── migration/
│
├── tests/
│   ├── architecture/
│   ├── contracts/
│   └── integration/
│
└── .github/
    └── workflows/
```

Essa estrutura é uma direção inicial, não autorização para criar diretórios vazios sem função.

O agente responsável pela fundação pode refiná-la, desde que preserve os conceitos e registre a decisão.

## 11.2 Histórico Git

A migração de Lunet2D e Urbe deve preservar o histórico relevante de ambos os projetos.

Não é aceitável simplesmente copiar os snapshots atuais e descartar a rastreabilidade existente.

A estratégia de importação deve ser documentada antes da execução.

## 11.3 Releases

Cada produto mantém:

- versão própria;
- changelog próprio quando aplicável;
- pipeline próprio;
- artefatos próprios;
- release própria.

Um commit no Urbe não implica uma nova versão do Lunet.

Um commit no Lunet não implica uma nova versão do Hub.

O monorepo possui um histórico comum, mas produtos possuem lifecycle independente.

---

# 12. Boundaries

O repositório deve possuir regras verificáveis de dependência.

Regra básica:

```text
apps/lunet2d ─┐
apps/urbe     ├──► platform/contracts
apps/hub      ┘
```

Proibido:

```text
Urbe → Lunet2D
Lunet2D → Urbe
Tool → Host concreto
Shared component → Product
```

Permitido:

```text
Product → Contract
Host → Contract
Tool → Contract
Adapter → Contract
```

Exceções devem ser explícitas e justificadas em ADR.

Boundaries importantes devem ser verificados por CI, não apenas descritos em documentação.

---

# 13. Não criar uma pasta "shared" genérica

Não deve existir uma pasta genérica usada como depósito de código que "talvez seja reutilizável".

Algo só deve entrar na plataforma compartilhada quando:

1. possui responsabilidade clara;
2. possui contrato claro;
3. sua dependência é arquiteturalmente correta;
4. há consumidor real ou forte necessidade transversal;
5. a extração reduz acoplamento em vez de ocultá-lo.

A criação prematura de abstrações é considerada risco arquitetural.

---

# 14. Comunicação runtime entre sistemas

GitHub não é barramento de runtime.

O fluxo abaixo é proibido como mecanismo de comunicação operacional:

```text
Urbe → GitHub → Lunet
```

O Ecosystem deve possuir contratos locais e transportes adequados para comunicação entre processos ou componentes.

O protocolo deve separar:

- command;
- event;
- request;
- response;
- capability discovery;
- permission;
- error;
- cancellation;
- progress.

O transporte não deve vazar para o contrato.

A mesma semântica deve poder usar, quando apropriado:

- comunicação in-process;
- IPC local;
- socket local;
- pipe;
- outro transporte futuro.

Toda mensagem runtime relevante deve possuir envelope versionado.

Exemplo conceitual:

```json
{
  "protocol": "ecosystem/1",
  "type": "command",
  "id": "...",
  "source": "...",
  "targetCapability": "sprite.edit",
  "operation": "open",
  "context": {},
  "permissions": []
}
```

A especificação definitiva deve ser criada antes da primeira dependência séria entre processos.

---

# 15. Hub como Host geral

O Hub deve ser capaz de hospedar workspaces e ferramentas compatíveis.

Exemplo:

```text
Hub
└── Sprite Studio
```

O Sprite Studio também deve poder ser hospedado por:

```text
Lunet2D
└── Sprite Studio
```

A capacidade de embutir UI depende do contrato de Host.

A arquitetura não deve assumir que "standalone" e "embedded" são aplicações completamente diferentes.

---

# 16. Hub como Launcher

O usuário deve poder utilizar o Hub como porta de entrada para os produtos.

Fluxo alvo:

```text
Abrir Hub
↓
Selecionar Lunet2D
↓
Hub compara versão local e release
↓
Se necessário, baixa a atualização
↓
Valida integridade
↓
Conduz a atualização pelo mecanismo permitido pelo sistema
↓
Abre Lunet2D
```

No Android, o Ecosystem deve respeitar o modelo de segurança da plataforma.

O projeto não deve ser arquitetado supondo instalação silenciosa de APKs quando o sistema operacional exige interação ou autorização.

---

# 17. GitHub como registro do desenvolvimento

GitHub é o registro remoto principal de desenvolvimento e distribuição do ecossistema.

O Hub deve conseguir inspecionar, progressivamente:

- commit atual;
- histórico;
- branches;
- pull requests;
- issues;
- CI;
- GitHub Actions;
- releases;
- artefatos;
- tags;
- ROADMAP;
- SPEC;
- AGENTS;
- ADRs;
- changelogs;
- decisões;
- tarefas em andamento.

GitHub serve para reconstruir a história e o estado do desenvolvimento.

Não serve como substituto para IPC entre aplicativos.

---

# 18. Past / Now / Next

Todo produto deve ser representável pelo Hub em três dimensões:

## PAST

O que já aconteceu.

Exemplos:

- fases concluídas;
- commits;
- releases;
- decisões;
- bugs corrigidos;
- migrações;
- validações.

## NOW

O que está acontecendo.

Exemplos:

- fase atual;
- tarefas abertas;
- agentes ativos;
- branches;
- PRs;
- CI;
- bloqueios;
- arquivos impactados;
- decisões aguardando proprietário.

## NEXT

O que deve acontecer em seguida.

Exemplos:

- próximos itens de ROADMAP;
- dependências;
- gates;
- riscos;
- tarefas prontas;
- decisões necessárias.

O Hub não deve fabricar o "Next".

Ele deve derivá-lo das fontes de verdade do repositório.

---

# 19. Observabilidade dos agentes

Nenhum agente deve realizar trabalho invisível.

Toda execução relevante deve permitir descobrir:

```text
Agent ID
Projeto
Tarefa
Estado
Branch
Commit
PR
Arquivos impactados
Última ação observável
Próxima ação declarada
Bloqueios
CI
Resultado
Custos, quando aplicável
```

Estados mínimos sugeridos:

```text
planned
claimed
working
waiting
blocked
verifying
review
done
failed
cancelled
```

Os nomes definitivos devem ser formalizados em contrato.

---

# 20. Agent Workspace

O Agent Workspace deve ser uma capability independente.

Ele não é "IA do Urbe" nem "IA do Lunet".

Ele deve possuir arquitetura própria e poder receber contexto de diferentes produtos.

Conceitualmente:

```text
Agent Runtime
├── Model Gateway
├── Providers
├── Model Capabilities
├── Context Engine
├── Agent Runtime
├── Tool Registry
├── Permissions
├── Verification
├── Checkpoints
├── Observability
├── Cost Tracking
└── bridges
```

Exemplo:

```text
Agent Workspace
Contexto: Lunet2D/MyGame

Tools:
- read_code
- edit_code
- compile
- test
- run
- inspect_runtime
- asset operations
```

Mudando o contexto:

```text
Agent Workspace
Contexto: Urbe/Vault

Tools:
- read_note
- write_note
- search
- inspect_links
- create_page
```

O Agent Runtime permanece conceitualmente o mesmo.

Os tools disponíveis mudam de acordo com contexto, provider e permissões.

---

# 21. Humanos, agentes e plugins

Sempre que tecnicamente razoável, humanos, agentes e plugins devem operar sobre as mesmas operações fundamentais.

Não devem existir três implementações divergentes para:

```text
humano salva arquivo
agente salva arquivo
plugin salva arquivo
```

Deve existir uma operação canônica, sujeita às permissões e contexto adequados.

Esse princípio reduz divergência de comportamento e aumenta a testabilidade.

---

# 22. Comunicação Humano ↔ Máquina

A comunicação entre o proprietário e agentes é parte da arquitetura do projeto.

Ela não pode depender de memória informal de uma conversa.

## 22.1 Regra de persistência

Toda decisão material tomada em conversa e utilizada para implementação deve ser registrada no repositório ou no sistema canônico correspondente antes de se tornar base arquitetural permanente.

"Eu lembro que o usuário queria isso" não é fonte de verdade.

## 22.2 Categorias de mensagem

Comunicação relevante deve poder ser classificada como:

```text
QUESTION
PROPOSAL
DECISION_REQUIRED
DECISION
BLOCKER
RESULT
INCIDENT
HANDOFF
```

## 22.3 Decisão humana

Quando uma decisão for necessária, o agente deve apresentar:

- contexto;
- decisão exata necessária;
- alternativas reais;
- consequências;
- compatibilidade com este manifesto;
- recomendação técnica, quando útil;
- pergunta objetiva.

O agente não deve transformar preferência própria em decisão do proprietário.

## 22.4 Autonomia

A necessidade de comunicação não deve transformar o proprietário em gargalo para decisões triviais.

Agentes podem decidir autonomamente detalhes de implementação que:

- estejam dentro dos contratos aprovados;
- não mudem produto;
- não mudem boundary;
- não criem incompatibilidade;
- não apaguem dados;
- não alterem decisão consolidada;
- não adicionem tecnologia estrutural sem necessidade.

Mudanças arquiteturais, de produto ou de contrato exigem processo explícito.

---

# 23. Comunicação Máquina ↔ Máquina

Agentes diferentes devem poder continuar o trabalho uns dos outros sem depender de memória privada.

A comunicação entre agentes deve ser:

- explícita;
- durável;
- identificável;
- vinculada a tarefa;
- vinculada a evidência;
- livre de raciocínio interno privado;
- adequada a leitura humana e processamento automático.

## 23.1 Handoff mínimo

Um handoff deve conter:

```text
message_id
timestamp
agent
task_id
component
state
branch
commit
pr
files_changed
work_completed
verification
known_issues
blockers
next_actions
decisions_required
```

## 23.2 Sem impersonação

Um agente nunca escreve uma mensagem fingindo ser outro agente.

Um agente nunca inventa revisão, aprovação ou decisão que não aconteceu.

## 23.3 Concorrência

Agentes devem coordenar trabalho concorrente por escopo, branch e arquivos.

Nenhum agente pode sobrescrever mudanças de outro apenas para resolver rapidamente uma divergência.

## 23.4 Canonicalidade

Mensagens entre agentes comunicam estado.

Elas não substituem:

- MANIFEST;
- SPEC;
- ADR;
- contratos;
- ROADMAP;
- testes.

---

# 24. Hierarquia de autoridade

Quando houver conflito, a seguinte ordem deve orientar a resolução:

1. decisão explícita e atual do proprietário, registrada;
2. `MANIFEST.md`;
3. ADRs aprovados;
4. `ARCHITECTURE.md` e contracts;
5. SPEC do componente;
6. ROADMAP;
7. tarefa/Issue;
8. handoffs e mensagens de agentes;
9. comentários informais;
10. inferências.

Uma nova decisão do proprietário pode mudar qualquer item abaixo, mas a documentação normativa afetada deve ser atualizada.

Nenhum agente pode usar uma fonte de menor autoridade para contrariar uma fonte superior.

---

# 25. AGENTS.md raiz

O monorepo deve possuir `AGENTS.md` na raiz.

Ele deve obrigar qualquer agente a:

1. ler `MANIFEST.md`;
2. ler integralmente a seção `NON-NEGOTIABLES`;
3. ler `ecosystem.json`;
4. identificar o componente afetado;
5. ler os documentos normativos daquele componente;
6. verificar estado atual do Git;
7. verificar branches/PRs/tarefas relevantes;
8. declarar o escopo da tarefa;
9. identificar quais `NN-XXX` são relevantes para a tarefa;
10. respeitar boundaries;
11. implementar;
12. testar;
13. verificar;
14. documentar;
15. registrar handoff/resultado;
16. não declarar conclusão sem evidência.

O `AGENTS.md` raiz deve conter uma seção própria chamada `NON-NEGOTIABLES` ou equivalente operacional que reproduza todas as invariantes `NN-XXX` sem alterar seu significado.

É permitido condensar explicações, mas não:

- remover uma invariante;
- suavizar uma proibição;
- alterar seu escopo;
- transformar obrigação em recomendação;
- omitir o mecanismo de enforcement;
- substituir a referência ao `MANIFEST.md`.

`AGENTS.md` pode ser operacional.

Ele não substitui este manifesto.

Quando uma tarefa estrutural tocar uma das invariantes, o handoff deve registrar os IDs `NN-XXX` considerados e as evidências de que foram respeitados.

---

# 26. Instruções locais de agentes

Componentes podem possuir `AGENTS.md` próprios.

Exemplo:

```text
/AGENTS.md
/apps/lunet2d/AGENTS.md
/apps/urbe/AGENTS.md
/apps/hub/AGENTS.md
```

A regra local complementa a raiz.

Ela não pode contradizê-la.

---

# 27. Tarefas e rastreabilidade

Trabalho significativo deve possuir uma identidade estável.

A rastreabilidade ideal é:

```text
Necessidade
↓
Decisão
↓
Requirement
↓
Roadmap item / Task
↓
Branch
↓
Commits
↓
Tests
↓
PR
↓
CI
↓
Merge
↓
Release
↓
Validation
```

Nem todo bug pequeno precisa de burocracia excessiva.

Mudanças estruturais sempre precisam de rastreabilidade completa.

---

# 28. Definition of Done

Uma tarefa não está concluída simplesmente porque arquivos foram modificados.

Quando aplicável, "Done" exige:

- implementação;
- integração;
- testes;
- verificação;
- documentação;
- boundaries respeitados;
- contratos atualizados;
- compatibilidade analisada;
- CI;
- ausência de regressão conhecida;
- evidência do resultado;
- validação humana quando o comportamento depende de dispositivo ou experiência visual.

"CI verde" não substitui validação que só pode ser feita no aparelho.

---

# 29. ADRs

Decisões estruturais devem usar Architecture Decision Records.

ADR é necessário, por exemplo, para:

- protocolo IPC;
- formato de manifest;
- modelo de capability;
- linguagem/runtime excepcional;
- boundaries;
- packaging;
- transporte;
- modelo de plugins;
- versionamento;
- compatibilidade;
- migração de dados;
- mudança de arquitetura do Urbe;
- mudança de arquitetura do Lunet;
- escolha estrutural do Hub.

Um ADR deve documentar:

- contexto;
- problema;
- opções;
- decisão;
- consequências;
- alternativas rejeitadas;
- status.

---

# 30. Segurança e permissões

Capabilities devem operar sob permissões explícitas.

Exemplos:

```text
filesystem.read
filesystem.write
project.read
project.write
network
git.read
git.write
github.read
github.write
build
agent.tools
assets.read
assets.write
```

Não é necessário usar exatamente esses nomes no contrato final.

É necessário manter o princípio.

## 30.1 Menor privilégio

Uma Tool não deve receber acesso irrestrito ao sistema apenas porque foi aberta por um Host confiável.

## 30.2 Credenciais

Tokens, API keys, signing keys e segredos:

- nunca devem ser commitados;
- nunca devem entrar em logs;
- nunca devem entrar em handoffs;
- nunca devem ser incorporados em projeto exportado;
- devem utilizar armazenamento seguro apropriado à plataforma.

## 30.3 Agentes

Agentes não recebem automaticamente todas as permissões disponíveis ao usuário.

Permissão e contexto precisam ser explícitos.

---

# 31. Versionamento

Cada componente público deve possuir versão quando existir compatibilidade a preservar.

Mudanças incompatíveis devem ser detectáveis.

O monorepo não significa "uma versão para tudo".

Devem existir versões independentes quando os lifecycles forem independentes.

Contracts públicos precisam possuir política explícita de compatibilidade.

---

# 32. CI seletivo e transversal

O CI deve entender o grafo do ecossistema.

Uma mudança em:

```text
apps/urbe/**
```

não deveria reconstruir tudo desnecessariamente.

Uma mudança em:

```text
platform/contracts/**
```

deve testar todos os consumidores impactados.

Portanto o objetivo é:

```text
mudança
↓
grafo de dependência
↓
componentes afetados
↓
testes/builds necessários
```

O grafo deve ser derivado de manifests e contratos, não de uma lista manual impossível de manter.

---

# 33. Hub: visão de arquitetura

O Hub deve possuir uma área capaz de mostrar o grafo do ecossistema.

Exemplo:

```text
                Ecosystem.Contracts
                 /       |       \
                /        |        \
               ▼         ▼         ▼
          Agent Runtime Editor    Registry
              │           │
          ┌───┴───┐       │
          ▼       ▼       ▼
        Urbe   Lunet2D    Hub
```

Ao selecionar um componente, o usuário deve poder descobrir:

- versão;
- consumidores;
- providers;
- dependências;
- documentação;
- mudanças recentes;
- problemas;
- impacto potencial;
- release;
- CI.

---

# 34. Hub: visão de desenvolvimento

O Hub deve eventualmente oferecer uma timeline semelhante a:

```text
10:32  tarefa iniciada
10:38  commit criado
10:41  CI falhou
10:46  correção enviada
10:49  CI verde
10:51  PR criado
11:05  merge
11:07  release gerada
11:25  validação em aparelho pendente
```

A timeline deve apontar para evidências.

Ela não pode inventar causalidade.

---

# 35. Hub: decisões pendentes

O Hub deve possuir uma superfície específica para responder:

> "Onde o ecossistema precisa de mim?"

Exemplos:

```text
DECISÕES NECESSÁRIAS

Lunet2D
- aceitar breaking change no Plugin Contract?

Urbe
- manter compatibilidade com formato legado X?

Hub
- escolher política de update channel?
```

Essa área deve ser diferenciada de:

- erros;
- tarefas;
- sugestões;
- notificações.

Uma decisão pendente é uma pergunta ao proprietário que bloqueia ou altera direção.

---

# 36. Hub: sidebar conceitual

A organização conceitual inicial pode ser:

```text
Apps
  Lunet2D
  Urbe

Workspaces
  Agent
  Editor
  GitHub
  Documentation

Tools
  Sprite Studio
  ...

Control
  Overview
  Past / Now / Next
  Agents
  CI
  Releases
  Tasks
  Decisions
  Architecture
  Dependencies
```

A UI final pode mudar.

A separação conceitual entre Products, Workspaces, Tools e Control deve permanecer clara.

---

# 37. Editor

O editor não deve ser extraído do Lunet apenas por desejo de modularização.

Primeiro, suas fronteiras internas devem ser claras.

Direção conceitual:

```text
Editor.Core
Editor.CSharp
Editor.Host
Editor.SDK
```

Quando existir contrato estável, o mesmo Editor poderá ser hospedado:

- pelo Lunet;
- pelo Hub;
- por outro Host compatível.

O produto não deve ser quebrado durante a extração.

---

# 38. Sprite Studio

O Sprite Studio é o exemplo de referência de Tool reutilizável.

Ele deve ser projetado sem dependência direta do Lunet.

Pode depender de capabilities genéricas como:

```text
file.read
file.write
asset.context
history
agent.tools
```

Ele pode ganhar integração rica com Lunet através de capabilities opcionais.

Abrir no Hub sem Lunet instalado deve ser um caso de uso válido.

---

# 39. Agent Runtime como infraestrutura compartilhada

O Agent Runtime será uma das primeiras infraestruturas transversais importantes.

O Urbe já possui conceitos de providers, tools e agentes.

O Lunet2D possui especificação para um Agentic Workspace.

O Ecosystem deve impedir que ambos evoluam em runtimes incompatíveis sem necessidade.

A estratégia deve ser:

1. mapear o que já existe;
2. definir contratos comuns;
3. preservar comportamento atual;
4. extrair semanticamente o que é compartilhável;
5. criar adapters;
6. testar cada consumidor;
7. remover duplicação apenas quando a substituição estiver comprovada.

Não deve existir uma reescrita cega da IA do Urbe.

---

# 40. Desenvolvimento local-first

Ferramentas essenciais devem preferir funcionamento local quando seu domínio permitir.

O Ecosystem não deve transformar ferramentas locais em dependências de um servidor apenas para facilitar integração.

Especialmente:

- Lunet core;
- editor;
- projetos;
- documentação;
- ferramentas locais;
- Urbe vault;
- arquivos do usuário.

Recursos online podem adicionar:

- GitHub;
- modelos remotos;
- downloads;
- releases;
- serviços de comunidade;
- multiplayer;
- outros serviços futuros.

Ausência de internet não deve inutilizar artificialmente funcionalidades locais.

---

# 41. Dados pertencem ao usuário

Arquivos de projeto, documentos, assets e dados primários devem permanecer sob controle do usuário.

O ecossistema não deve criar dependência artificial de conta ou assinatura para acesso aos próprios dados.

Futuros modelos comerciais não podem sequestrar projetos.

---

# 42. O Hub não é o banco de dados universal

O Hub pode manter índice, cache e metadados.

Ele não deve se tornar proprietário dos dados internos de todos os produtos.

Urbe continua responsável por seu domínio.

Lunet continua responsável por projetos de jogos.

Ferramentas continuam responsáveis apenas por seus formatos e contratos.

---

# 43. Não duplicar autoridade

Para cada conceito persistido ou operacional deve existir uma autoridade clara.

Exemplo:

```text
versão do produto → uma fonte canônica
estado da tarefa → uma fonte canônica
manifest de capability → uma fonte canônica
roadmap → uma fonte canônica
```

Caches e projeções podem existir.

Eles nunca devem silenciosamente virar uma segunda autoridade.

---

# 44. Migração para o monorepo

A migração dos projetos existentes deve ser tratada como projeto próprio.

## 44.1 Antes da migração

Obrigatório documentar:

- repositórios de origem;
- branches relevantes;
- tags;
- releases;
- workflows;
- GitHub Pages, se houver;
- secrets/configuração necessária;
- dependências externas;
- arquivos normativos;
- status atual;
- estratégia de preservação do histórico.

## 44.2 Durante a migração

Proibido:

- reescrever Urbe;
- reescrever Lunet;
- "limpar" código só porque está sendo movido;
- alterar comportamento funcional sem item específico;
- perder histórico;
- perder release;
- misturar refactor com importação.

## 44.3 Após a migração

Cada produto precisa provar:

- build;
- testes;
- workflows;
- release;
- documentação;
- paths;
- dados;
- links;
- processo de desenvolvimento.

Somente então o repositório antigo pode receber status de legado/arquivado, quando o proprietário decidir.

---

# 45. O primeiro commit do Ecosystem

O primeiro commit não precisa conter Urbe e Lunet2D importados.

Ele deve formalizar a fundação.

Deve incluir, no mínimo:

```text
MANIFEST.md
README.md
AGENTS.md
ARCHITECTURE.md
ROADMAP.md
ecosystem.json
docs/adr/
docs/contracts/
docs/governance/
docs/migration/
.github/
```

Podem existir placeholders estruturais apenas quando tiverem função explícita.

Não criar dezenas de projetos C# vazios apenas para "parecer completo".

## 45.1 Objetivo do primeiro commit

Após o primeiro commit, outro agente deve conseguir responder:

- o que é o Ecosystem;
- quais são os produtos principais;
- o que é um Host;
- o que é Capability;
- o que é Tool;
- o que é Service;
- o que é Workspace;
- quais dependências são permitidas;
- qual linguagem é padrão;
- quais são todas as invariantes `NN-XXX`;
- como cada invariante crítica é fiscalizada;
- como decisões são tomadas;
- como agentes se comunicam;
- como os projetos serão migrados;
- qual é a próxima etapa.

O primeiro commit também deve introduzir uma forma explícita de validar a matriz de enforcement, mesmo que inicialmente parte dela ainda seja documental/manual.

---

# 46. Fases iniciais

## Fase 0 — Constituição

Objetivo:

- criar monorepo;
- incluir documentos normativos;
- criar `ecosystem.json`;
- definir processo de ADR;
- definir comunicação humano↔máquina;
- definir comunicação máquina↔máquina;
- definir boundaries;
- configurar CI mínimo de consistência.

Gate:

> Um agente novo consegue entrar no repositório e compreender corretamente o produto, a autoridade documental e o processo de trabalho sem depender de uma conversa anterior.

## Fase 1 — Inventário e migração

Objetivo:

- auditar Lunet2D;
- auditar Urbe;
- mapear históricos;
- mapear workflows;
- preparar importação;
- migrar preservando histórico;
- restaurar builds/testes/releases.

Gate:

> Lunet2D e Urbe vivem no monorepo sem regressão conhecida e continuam possuindo lifecycles próprios.

## Fase 2 — Contracts e Registry

Objetivo:

- formalizar `ComponentManifest`;
- formalizar `Capability`;
- formalizar provider/consumer;
- definir versionamento;
- definir permission model;
- criar registry inicial;
- criar testes de boundary.

Gate:

> Um componente pode declarar uma capability, outro pode descobri-la e a compatibilidade pode ser validada sem dependência direta entre produtos.

## Fase 3 — Hub read-only

Objetivo:

- criar Hub inicial em C#;
- ler `ecosystem.json`;
- mostrar Lunet2D e Urbe;
- integrar leitura do GitHub;
- implementar Past / Now / Next;
- visualizar CI, releases, branches, PRs e tarefas.

Gate:

> No celular, o proprietário abre o Hub e compreende o estado atual dos dois produtos sem abrir GitHub manualmente.

## Fase 4 — Launcher e Updates

Objetivo:

- detectar versões;
- listar releases;
- baixar artefatos;
- validar integridade;
- conduzir instalação/atualização;
- abrir produto instalado.

Gate:

> O usuário utiliza o Hub como entrada para instalar/atualizar/abrir Lunet2D e Urbe dentro dos limites da plataforma.

## Fase 5 — Capability Runtime

Objetivo:

- definir IPC;
- command/event/request;
- capability discovery local;
- lifecycle;
- permissions;
- Host API;
- Tool hosting.

Gate:

> Uma Tool simples pode funcionar standalone no Hub e embutida em outro Host sem código específico para aquele Host.

## Fase 6 — Agent Workspace compartilhado

Objetivo:

- mapear IA do Urbe;
- mapear Agentic Workspace do Lunet;
- definir Agent Runtime comum;
- implementar/adaptar contratos;
- preservar integrações específicas.

Gate:

> O mesmo modelo conceitual de Agent Workspace pode operar com contexto do Urbe ou Lunet por tools/capabilities diferentes.

## Fase 7 — Primeira Tool real compartilhada

Preferencialmente escolher uma ferramenta com boundary claro, por exemplo Sprite Studio ou Editor, após auditoria.

Gate:

> A ferramenta funciona standalone e integrada sem duplicação de implementação.

---

# 47. O que NÃO fazer no início

É proibido tratar os itens abaixo como objetivo da fundação:

- reescrever Urbe em C#;
- mover toda função do Lunet para packages;
- criar marketplace;
- criar rede social;
- criar vinte microserviços;
- criar backend obrigatório;
- criar sistema de conta;
- criar abstrações sem consumidor;
- implementar todas as tools;
- criar capability para cada função;
- transformar Hub em dependência central;
- substituir todos os roadmaps existentes;
- apagar documentação histórica;
- padronizar tudo pela força.

A fundação existe para permitir evolução segura, não para reconstruir o mundo de uma vez.

---

# 48. Critério para criar um novo componente compartilhado

Antes de criar componente compartilhado, responder:

1. Qual responsabilidade única ele possui?
2. Por que não pertence ao produto atual?
3. Há mais de um consumidor?
4. Qual é seu contrato?
5. Quem é dono dos dados?
6. Quais permissões precisa?
7. Pode funcionar standalone?
8. Pode ser embedded?
9. Como é versionado?
10. O que acontece se estiver ausente?
11. Qual a compatibilidade mínima?
12. Qual teste prova sua independência?

Se as respostas forem ruins, não extrair ainda.

---

# 49. Critério para uma nova dependência

Antes de adicionar dependência tecnológica:

- qual problema real resolve;
- por que a plataforma atual não resolve;
- impacto em Android;
- impacto em offline;
- impacto em tamanho;
- impacto em build;
- impacto em segurança;
- impacto em manutenção;
- licença;
- risco de abandono;
- possibilidade de substituição.

"É moderno" não é justificativa.

---

# 50. Critério de qualidade para agentes

Um agente de alta autonomia é útil apenas se seu trabalho for auditável.

Portanto:

> autonomia sem observabilidade é defeito.

E:

> velocidade sem preservação de intenção é regressão.

E:

> código produzido não é evidência suficiente de tarefa concluída.

---

# 51. Regra contra desvio gradual

O maior risco de um projeto autônomo de longa duração não é apenas um grande erro.

É o desvio acumulado de pequenas interpretações.

Para combater isso:

- requisitos estruturais devem possuir IDs;
- decisões devem ser registradas;
- agents devem citar o requisito/tarefa afetado;
- CI deve verificar boundaries;
- documentação deve ser atualizada junto do código;
- dívida criada deve ser declarada;
- mudanças de arquitetura devem gerar ADR;
- handoffs devem registrar limitações;
- nenhuma conclusão pode ser baseada apenas em "parece funcionar".

---

# 52. Regra contra compressão destrutiva

Agentes frequentemente resumem documentos longos para caber em contexto.

Isto é permitido para navegação.

Não é permitido usar o resumo como substituto de cláusulas normativas.

Quando um agente precisar modificar arquitetura, governança, protocolo, migração ou contract, ele deve consultar diretamente os documentos normativos relevantes.

A ausência de uma cláusula no resumo não significa que a cláusula deixou de existir.

---

# 53. Regra contra contexto falso

Um agente deve separar:

```text
FATO OBSERVADO
INFERÊNCIA
PROPOSTA
DECISÃO
```

Não é aceitável afirmar estado de código sem verificar.

Não é aceitável transformar inferência em requisito.

Não é aceitável transformar proposta em decisão.

Não é aceitável transformar decisão antiga em decisão atual sem conferir sua validade.

---

# 54. Regra de evolução incremental

Grandes migrações devem ser compostas por mudanças verificáveis e reversíveis quando possível.

Especialmente:

- Urbe;
- Lunet;
- Agent Runtime;
- Editor;
- Tool extraction;
- manifests;
- IPC.

Nova implementação só substitui autoridade antiga depois de provar equivalência suficiente.

---

# 55. Experiência desejada no futuro

O usuário pega o celular.

Abre o Ecosystem Hub.

Vê:

```text
APPS

Lunet2D
Instalado: 0.x
Último: 0.y
[Atualizar] [Abrir]

Urbe
Instalado: 2.x
Atualizado
[Abrir]
```

Na lateral:

```text
Workspaces
- Agent
- Editor
- GitHub
- Documentation

Tools
- Sprite Studio
- ...

Control
- Overview
- Agents
- Tasks
- CI
- Releases
- Decisions
- Architecture
```

Ele pode abrir Sprite Studio sem Lunet instalado.

Ele pode instalar Lunet e conectar Sprite Studio.

Ele pode abrir Agent Workspace sozinho.

Ele pode conectar o Agent Workspace ao Lunet.

Ele pode trocar o contexto para Urbe.

Ele pode visualizar o que os agentes modificaram.

Ele pode descobrir quais decisões dependem dele.

Ele pode inspecionar toda a arquitetura.

Ele pode entender o passado, o presente e o próximo passo.

Esse é o produto.

---

# 56. Princípios finais

1. **C# é a linguagem padrão para nova infraestrutura.**
2. **Urbe e Lunet2D são produtos, não módulos do Hub.**
3. **Hub é Control Plane e Host geral, não ponto único de falha.**
4. **Capabilities são contratos, não atalhos para acoplamento.**
5. **Tools reutilizáveis não pertencem artificialmente a um produto.**
6. **Uma Tool deve ignorar qual Host concreto a chamou.**
7. **Produtos essenciais funcionam mesmo sem capabilities opcionais.**
8. **GitHub registra desenvolvimento; não substitui IPC.**
9. **O monorepo compartilha história e coordenação, não lifecycle de release.**
10. **Contracts públicos são versionados e testados.**
11. **Boundaries importantes são verificadas automaticamente.**
12. **Não existe `shared` como depósito de código sem dono.**
13. **Dados pertencem ao usuário.**
14. **Offline continua sendo um valor arquitetural.**
15. **Humano, agente e plugin compartilham operações canônicas sempre que possível.**
16. **Toda decisão material precisa sobreviver à conversa que a originou.**
17. **Toda máquina precisa conseguir explicar o estado que produziu.**
18. **Todo agente precisa deixar um handoff compreensível para outro agente.**
19. **Nenhum agente pode substituir este manifesto por sua interpretação resumida.**
20. **Autonomia exige observabilidade, rastreabilidade e possibilidade de correção.**
21. **O ecossistema deve crescer por composição, não por monólito.**
22. **A arquitetura existe para manter o proprietário no controle mesmo quando o software e os agentes ganham autonomia.**

---

# 57. Instrução para o agente fundador

O agente que receber este documento para criar o repositório inicial deve:

1. tratá-lo como fonte normativa;
2. não implementar o produto inteiro;
3. criar a fundação documental e estrutural da Fase 0;
4. propor qualquer alteração necessária antes de contradizer uma cláusula;
5. criar `AGENTS.md` coerente com este manifesto e reproduzir operacionalmente todas as invariantes `NN-XXX`, sem paráfrase destrutiva;
6. criar uma matriz de enforcement das invariantes, indicando o que é fiscalizado por DOC/SCHEMA/TEST/ARCH/CI/REVIEW/ADR/DEVICE/RUNTIME;
7. criar `ARCHITECTURE.md` inicial sem inventar decisões não tomadas;
8. criar `ROADMAP.md` baseado nas fases deste documento;
9. criar `ecosystem.json` inicial;
10. criar processo de ADR;
11. criar regras de comunicação humano↔máquina e máquina↔máquina;
12. criar checks mínimos de consistência;
13. documentar a estratégia de migração antes de importar Urbe ou Lunet2D;
14. preservar a independência dos dois produtos;
15. não iniciar uma reescrita;
16. não criar scaffolding excessivo;
17. realizar o primeiro commit com mensagem que indique explicitamente a fundação do Ecosystem;
18. deixar o repositório em um estado no qual o próximo agente saiba exatamente qual é a próxima tarefa e por quê.

O primeiro commit representa o início formal do Ecosystem.

Ele não representa a conclusão de sua arquitetura.

---

# 58. Declaração

O Ecosystem existe para permitir que aplicativos, ferramentas, agentes e humanos cooperem sem que a complexidade acumulada destrua a compreensão do sistema.

Seu objetivo não é centralizar todo o código.

Seu objetivo é centralizar **compreensão, contratos, descoberta, coordenação e controle**, enquanto preserva a independência dos componentes.

O sucesso do projeto não será medido pelo número de módulos, ferramentas ou agentes.

Será medido por uma propriedade mais difícil:

> mesmo quando o ecossistema for grande, o proprietário ainda consegue abrir uma única superfície, compreender o que existe, entender o que aconteceu, saber o que está acontecendo, descobrir o que acontecerá depois e intervir com segurança quando quiser.
