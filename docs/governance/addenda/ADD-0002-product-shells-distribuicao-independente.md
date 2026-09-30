# ADD-0002 — Product Shells, distribuição independente e plataforma própria

> **Tipo:** decisão explícita do proprietário, registrada (MANIFEST §24, nível 1).
> **Autor:** AbnerCruz (proprietário). **Recebido em:** 2026-09-30, como instrução de tarefa ao agente `claude-code`, e persistido aqui conforme NN-009.
> **Registro de decisão:** DEC-0006 em [`../decisions.json`](../decisions.json). **Formalização:** [ADR-0006](../../adr/0006-product-shell-context-distribuicao-independente.md), [`product-model.md`](../../architecture/product-model.md), [`distribution.md`](../../architecture/distribution.md), [`product-vision.md`](../../architecture/product-vision.md), [`faq.md`](../../architecture/faq.md); invariante **NN-023** no `MANIFEST.md` ([registro da alteração](../manifest-changelog.md)).
> **Relação com o MANIFEST:** decisão posterior do proprietário. O item §5 deste texto determina explicitamente a atualização do `MANIFEST.md` com uma nova invariante; nenhuma invariante existente foi alterada ou renumerada.
> **Escopo do texto:** o texto é uma instrução de tarefa. As decisões do proprietário nele contidas estão enumeradas em DEC-0006; o que o texto não decide continua pendente ou sujeito a ADR (§20 do próprio texto). Em particular, **não resolve DEC-0001, DEC-0002, DEC-0003 nem DEC-0004.**
>
> O texto abaixo é a transcrição integral, sem alterações. Não editar; mudanças exigem novo adendo ou decisão registrada.

---

# Adendo arquitetural — Product Shells, distribuição independente e plataforma própria

Você está trabalhando no repositório `AbnerCruz/Ecosystem`.

Antes de alterar qualquer coisa, siga integralmente o protocolo já existente no repositório:

1. leia `MANIFEST.md` integralmente;
2. leia `AGENTS.md`;
3. leia `ARCHITECTURE.md`;
4. leia `ROADMAP.md`;
5. leia `ecosystem.json`;
6. leia os ADRs existentes;
7. leia `docs/governance/decisions.json`;
8. leia os handoffs relevantes;
9. leia `docs/migration/README.md`;
10. leia `docs/migration/inventory-template.md`;
11. rode os checks atuais antes de modificar qualquer coisa;
12. respeite todos os `NN-XXX`.

Não substitua essas fontes por este prompt.

Este prompt é uma **decisão posterior do proprietário** e deve ser persistido segundo o próprio protocolo de governança do Ecosystem.

O objetivo desta tarefa é formalizar uma evolução importante da visão do produto **antes de começar a migração de Lunet2D e Urbe**.

Não importe Lunet2D.

Não importe Urbe.

Não refatore os repositórios de origem.

Não crie scaffolding vazio.

Não implemente Store, marketplace, comunidade, runtime de capabilities ou Hub completo agora.

Esta tarefa é de **alinhamento arquitetural, documentação, governança e preparação da migração**.

---

# 1. Novo modelo mental do Ecosystem

A arquitetura passa a possuir explicitamente três níveis principais de experiência:

```text
LEVEL 1
ECOSYSTEM HUB
universo geral do proprietário/ecossistema

        ↓

LEVEL 2
PRODUCT SHELL
universo especializado de um produto

        ↓

LEVEL 3
WORKSPACE / TOOL
atividade concreta do usuário
```

Exemplos:

```text
Ecosystem
→ Lunet2D
→ MeuJogo
→ Editor
```

```text
Ecosystem
→ Lunet2D
→ MeuJogo
→ Sprite Studio
```

```text
Ecosystem
→ Urbe
→ Meu Vault
→ Editor
```

Também deve ser possível:

```text
Ecosystem
→ Sprite Studio
```

quando a Tool puder funcionar de forma independente.

Portanto, Product Shell não transforma Tools reutilizáveis em propriedade privada do produto.

---

# 2. Formalizar o conceito de Product Shell

Adicionar formalmente à arquitetura o conceito de:

`Product Shell`

Definição desejada:

> Product Shell é a superfície especializada e Host de um Product, responsável por representar seu domínio, navegação, biblioteca, projetos, integrações e capabilities disponíveis, preservando uma experiência própria sem possuir artificialmente as Tools reutilizáveis que hospeda.

O Product Shell:

- possui identidade visual e UX próprias;
- conhece o domínio do produto;
- organiza funcionalidades daquele domínio;
- pode hospedar Workspaces e Tools;
- pode consumir Services;
- pode descobrir capabilities;
- pode apresentar Store, Library, Community e outras superfícies próprias do produto;
- pode funcionar como aplicação pública independente;
- não deve exigir o Ecosystem Hub;
- não deve fazer Tools reutilizáveis conhecerem o Host concreto.

Exemplos iniciais:

```text
Lunet Product Shell
Urbe Product Shell
```

Não é necessário criar diretórios ou código para esses Shells nesta tarefa.

Formalize o conceito primeiro.

---

# 3. Ecosystem Hub e Product Shell não são a mesma coisa

O Ecosystem Hub continua sendo:

- Control Plane;
- Host geral;
- Launcher;
- Registry;
- superfície transversal;
- ambiente de observação do ecossistema;
- ambiente do proprietário;
- possível ponto de entrada para produtos, Tools e Workspaces.

Mas ele NÃO é requisito para que um Product Shell seja distribuído.

O Product Shell deve poder existir publicamente sem que o Hub seja publicado.

Portanto é perfeitamente válido o seguinte cenário:

```text
INTERNO / PROPRIETÁRIO

Ecosystem Hub
├── Lunet2D
├── Urbe
├── Tools
├── Workspaces
├── agentes
├── observabilidade
└── administração
```

enquanto o público recebe apenas:

```text
Lunet2D
```

ou:

```text
Urbe
```

como aplicações completas.

---

# 4. Arquitetura não é distribuição

Formalizar explicitamente a diferença entre:

```text
componente existe no Ecosystem
```

e:

```text
componente é distribuído para determinado usuário
```

O fato de uma Tool, Service, Workspace ou Product existir no monorepo ou Registry NÃO significa automaticamente que:

- está incluído em todo produto;
- está instalado;
- está visível;
- está disponível publicamente;
- é gratuito;
- pode ser adquirido;
- pode ser usado naquele Host.

Introduzir conceitualmente um mecanismo equivalente a:

`Distribution Profile`

O nome definitivo pode ser refinado por ADR, mas o conceito precisa existir.

Um Distribution Profile descreve quais componentes formam determinada edição/distribuição.

Exemplo conceitual:

```text
lunet-public

Product:
Lunet2D

Bundled:
- Lunet Product Shell
- Lunet Framework
- Lunet Runtime
- Lunet Compiler
- Lunet Project System
- funcionalidades essenciais

Optional:
- Agent Workspace
- ferramentas adicionais
- plugins
- packages

Marketplace:
- assets
- templates
- plugins
- packages
- Tools compatíveis

Internal:
- administração geral do Ecosystem
- observabilidade cross-product do proprietário
- ferramentas privadas
```

Não implemente ainda o sistema de Distribution Profiles.

Formalize o conceito e planeje sua implementação para a fase apropriada.

---

# 5. Nova regra de independência de distribuição

Adicionar uma nova invariante `NN-XXX`, usando o próximo ID estável disponível, com significado equivalente a:

> Um Product declarado distribuível deve poder ser empacotado, publicado, instalado, atualizado e utilizado em seu domínio essencial sem exigir que o Ecosystem Hub seja distribuído ao usuário final.

Essa invariante complementa, mas não substitui, a regra atual de que o Hub não é dependência essencial.

Fiscalização futura deve considerar:

- dependency graph;
- packaging;
- release pipeline;
- testes standalone;
- validação de Distribution Profile;
- CI;
- DEVICE quando aplicável.

Atualize:

- `MANIFEST.md`;
- `AGENTS.md`;
- enforcement matrix;
- checks correspondentes quando já for possível fiscalizar algo automaticamente.

Não renumere invariantes existentes.

---

# 6. Formalizar Context

Adicionar à arquitetura o conceito explícito de `Context`.

Context representa o escopo atual no qual uma operação, Tool, Workspace ou Agent está trabalhando.

Exemplo:

```text
Ecosystem Context
```

```text
Ecosystem
→ Lunet2D
```

```text
Ecosystem
→ Lunet2D
→ MeuJogo
```

```text
Ecosystem
→ Lunet2D
→ MeuJogo
→ Editor
```

Outro exemplo:

```text
Ecosystem
→ Urbe
→ MeuVault
→ Nota X
```

O objetivo futuro é permitir que:

- Tools recebam contexto;
- Agents recebam contexto;
- permissões dependam do contexto;
- capability discovery seja contextual;
- UI possa priorizar capacidades relevantes;
- operações possam apontar precisamente para Product/Project/Vault/documento/asset atual.

Não defina prematuramente o contrato runtime nesta tarefa.

O contrato concreto pertence às fases de Contracts/Registry/Runtime.

Agora registre apenas o conceito e seus requisitos.

---

# 7. “Connections” é UX, não um novo sistema paralelo

Product Shells podem possuir uma superfície chamada:

`Connections`

Exemplo:

```text
Lunet2D
→ Connections

Editor
Sprite Studio
Agent Workspace
GitHub
Audio Studio
...
```

Mas `Connection` não deve virar uma arquitetura concorrente a Capabilities.

Por baixo, essa experiência deve ser implementável futuramente através de:

- Capability Registry;
- discovery;
- compatibility;
- permissions;
- installation/distribution state.

Portanto:

```text
Connections = experiência de usuário
Capabilities = mecanismo arquitetural
```

Registre essa distinção.

---

# 8. Redefinir a visão futura do Lunet2D

O Lunet2D atual é um ambiente de desenvolvimento de jogos 2D em C#, com framework, runtime, IDE/tooling e outras capacidades.

A visão futura é maior.

Formalizar:

> Lunet2D é uma plataforma completa de criação, desenvolvimento, execução, extensão, distribuição e comunidade para jogos 2D em C#, integrada ao Ecosystem, mas distribuível e utilizável de forma independente.

Conceitualmente:

```text
LUNET2D PLATFORM

Core
├── Lunet Framework
├── Compiler
├── Runtime
├── Project System
└── Build

Product Shell
├── Home
├── Projects
├── Library
├── Store
├── Community
├── Learn
├── Games
└── Connections

Catalog
├── Assets
├── Plugins
├── Templates
├── Packages
└── Tools

Reusable capabilities
├── Editor
├── Sprite Studio
├── Tilemap Studio
├── Agent Workspace
├── GitHub Workspace
└── outras capabilities
```

Isso é visão de produto.

NÃO implemente esses módulos agora.

NÃO presuma que todos já existem.

NÃO altere o Lunet atual para imitar imediatamente essa estrutura.

---

# 9. Store e Asset Store

A visão atual é que o Lunet possua um ecossistema comercial próprio.

Evitar construir duas infraestruturas completamente separadas para:

```text
Store
Asset Store
```

A direção conceitual é:

```text
Lunet Store
├── Assets
├── Plugins
├── Templates
├── Packages
└── Tools
```

Na UX pode existir uma área chamada `Asset Store`.

Mas tecnicamente Assets podem ser uma categoria do catálogo/marketplace geral do Lunet.

Nenhum backend, marketplace ou sistema comercial deve ser implementado nesta tarefa.

---

# 10. Store e Library são conceitos diferentes

Formalizar:

```text
Store
= conteúdo disponível para adquirir/instalar
```

```text
Library
= conteúdo que o usuário possui, instalou ou tem direito de usar
```

A Library do Lunet poderá futuramente conter:

- projetos;
- assets;
- plugins;
- templates;
- packages;
- Tools;
- conteúdo adquirido;
- downloads;
- atualizações.

Não implementar agora.

---

# 11. Plataforma first-party própria

Formalizar também a estratégia de distribuição atual do proprietário.

O Ecosystem deve permitir que o proprietário possua sua própria plataforma de:

- distribuição;
- downloads;
- atualizações;
- catálogo;
- identidade;
- biblioteca;
- entitlements/licenças;
- commerce;
- marketplace;
- comunidade.

Plataformas externas não são a autoridade arquitetural do produto.

A estratégia desejada é equivalente a:

> First-party by default; external distribution by choice.

Isso significa que uma plataforma externa pode, no futuro, receber:

- versão Starter;
- versão de divulgação;
- edição limitada;
- canal de aquisição;

enquanto a versão completa pode ser distribuída diretamente pela infraestrutura própria.

NÃO implementar pagamentos, contas, servidores ou marketplace agora.

Registrar apenas a direção do produto e os requisitos arquiteturais decorrentes.

Não transformar uma estratégia comercial atual em dependência técnica impossível de mudar futuramente.

---

# 12. Infraestrutura compartilhada, experiência própria

No futuro podem existir Services compartilhados como:

```text
Identity
Catalog
Commerce
Entitlements
Downloads
Updates
Reviews
Creator Profiles
Notifications
```

Esses serviços podem ser usados por múltiplos produtos.

Mas cada Product Shell apresenta uma experiência própria.

Exemplo:

```text
Lunet Store
```

pode usar:

```text
Catalog Service
Commerce Service
Entitlements Service
```

sem o usuário precisar conhecer esses nomes.

Da mesma forma, um futuro:

```text
Urbe Store
```

poderia reutilizar a infraestrutura sem compartilhar necessariamente o mesmo catálogo, política ou identidade visual.

Não criar esses Services agora apenas porque foram mencionados.

Aplicar rigorosamente NN-020/NN-022: extração só acontece quando reduz complexidade total e existem consumidores reais.

---

# 13. Hub privado é um cenário suportado

Formalizar explicitamente como cenário válido:

```text
Ecosystem Hub
visibility: private/internal
```

enquanto:

```text
Lunet2D
visibility: public
```

e:

```text
Urbe
visibility: public
```

O proprietário pode inclusive nunca distribuir o Ecosystem Hub publicamente.

Isso não pode impedir a distribuição completa de nenhum Product.

---

# 14. Metadata futura de disponibilidade

Durante a fase apropriada de Contracts/Registry/Packaging, considerar que componentes possam declarar conceitos equivalentes a:

```text
visibility
- public
- private
- internal
```

```text
distribution
- bundled
- optional
- marketplace
- unavailable
```

```text
commercial
- free
- paid
- subscription
- entitlement
```

Esses nomes NÃO são contrato final.

Não crie schema definitivo agora sem passar pelo processo de ADR da fase apropriada.

Registre apenas que a arquitetura precisa suportar esses três eixos distintos:

1. visibilidade;
2. forma de distribuição;
3. modelo comercial.

---

# 15. Urbe segue o mesmo princípio estrutural

O Urbe também deve ser tratável como Product completo com seu próprio Product Shell.

Exemplo conceitual:

```text
URBE PRODUCT SHELL

Home
Vault
World
Notes
Pages
Library
Extensions
Templates
Connections
```

Isso NÃO significa copiar a UI do Lunet.

Product Shell significa estrutura arquitetural comum, não UX igual.

Cada produto preserva:

- domínio;
- identidade;
- navegação;
- modelo mental;
- dados;
- experiência.

Urbe não vira um “skin” do Ecosystem.

Lunet não vira um “skin” do Ecosystem.

---

# 16. Atualizar a preparação da migração

A estratégia atual de migração está correta em separar:

```text
MIGRATION
```

de:

```text
REFACTOR / EXTRACTION / REDESIGN
```

PRESERVE isso.

Durante P1-1/P1-2, amplie o inventário para incluir um **mapa funcional e arquitetural**.

Para cada subsistema importante encontrado em Lunet2D e Urbe, registrar:

```text
Nome:
Responsabilidade atual:
Arquivos/diretórios:
Dependências:
Dados que possui:
UI:
Pode funcionar standalone hoje?
É específico do Product?
Dependências externas:
Riscos:
```

E uma classificação FUTURA, explicitamente marcada como proposta:

```text
Candidato futuro:
[ ] Product Core
[ ] Product Shell
[ ] Tool
[ ] Workspace
[ ] Service
[ ] Library
[ ] Adapter
[ ] Ainda indeterminado
```

Essa classificação é:

```text
PROPOSTA / INVENTÁRIO
```

não autorização para extrair.

Atualize `docs/migration/inventory-template.md`.

---

# 17. Regra crítica da migração

Durante a importação inicial de Lunet2D e Urbe:

PROIBIDO aproveitar a oportunidade para:

- separar Editor;
- extrair Sprite Studio;
- reescrever Agent Workspace;
- criar Store;
- criar Product Shell novo;
- transformar código em capabilities;
- reorganizar tudo em packages;
- reescrever Urbe em C#;
- mudar arquitetura interna apenas porque a arquitetura futura está conhecida.

A sequência correta é:

```text
1. INVENTARIAR
2. IMPORTAR
3. PRESERVAR HISTÓRICO
4. RESTAURAR BUILD
5. RESTAURAR TESTES
6. RESTAURAR RELEASES
7. VALIDAR PRODUTO
8. PROVAR AUSÊNCIA DE REGRESSÃO CONHECIDA
9. CLASSIFICAR CANDIDATOS
10. EXTRAIR/MODERNIZAR GRADUALMENTE
```

Adicionar isso explicitamente ao planejamento/gate da Fase 1.

---

# 18. Novo gate da Fase 1

Além do gate atual, acrescentar semanticamente:

> A importação preservou intencionalmente a arquitetura funcional existente. Candidatos a Product Core, Product Shell, Tool, Workspace, Service, Library e Adapter foram inventariados, porém nenhuma extração estrutural foi realizada como efeito colateral da migração.

Não marque isso concluído antes da auditoria pós-migração.

---

# 19. Atualizar o ROADMAP sem quebrar IDs existentes

Não renumere IDs existentes.

Adicione novas tarefas com IDs estáveis novos.

Antes de iniciar efetivamente P1-1/P1-2, devem existir tarefas para:

- persistir esta decisão do proprietário;
- atualizar MANIFEST;
- atualizar AGENTS;
- atualizar ARCHITECTURE;
- atualizar enforcement da nova invariante;
- atualizar ROADMAP;
- atualizar inventory template;
- documentar Product Shell;
- documentar Context;
- documentar Distribution Profile como conceito futuro;
- registrar visão futura do Lunet;
- registrar independência entre Store e Library;
- registrar estratégia de distribuição first-party;
- atualizar migration gate.

Se P1-1/P1-2 ainda não começaram, essas tarefas devem ser concluídas antes deles.

Se já começaram, não descarte o trabalho: adapte o inventário antes de prosseguir para importação.

---

# 20. Persistência desta decisão

Como esta instrução vem diretamente do proprietário:

- persista o texto integral ou uma representação fiel em `docs/governance/addenda/`;
- registre a decisão em `docs/governance/decisions.json` usando o próximo ID estável;
- crie ADR quando necessário para formalizar decisões estruturais;
- não invente aprovação para decisões que este texto não tomou;
- não use este prompt para resolver automaticamente DEC-0001, DEC-0002, DEC-0003 ou DEC-0004;
- decisões pendentes existentes continuam pendentes até manifestação explícita do proprietário.

O Product Shell, Context hierarchy, distribuição independente, suporte a Hub privado e estratégia first-party estabelecidos aqui são decisões do proprietário.

Detalhes técnicos ainda não decididos continuam sujeitos a ADR.

---

# 21. Não criar abstrações prematuras

Este alinhamento é propositalmente amplo.

Isso NÃO autoriza criar agora:

```text
platform/commerce
platform/identity
platform/store
platform/community
platform/entitlements
platform/distribution
tools/editor
tools/sprite-studio
workspaces/agent
```

sem implementação real, consumidor real, contrato e justificativa.

NN-020 e NN-022 continuam integralmente válidos.

Primeiro conheça o código real durante os inventários.

Depois decida o que realmente merece ser extraído.

---

# 22. Estado desejado depois desta tarefa

Depois deste trabalho, um novo agente deve conseguir responder corretamente:

1. O que é o Ecosystem Hub?
2. O que é um Product?
3. O que é um Product Shell?
4. O que é um Host?
5. O que é Context?
6. O que é uma Capability?
7. O que é uma Tool?
8. O que é um Workspace?
9. O que é um Service?
10. Qual a diferença entre arquitetura e distribuição?
11. Um Product pode ser publicado sem o Hub?
12. O Hub pode permanecer privado?
13. Lunet2D pode ser distribuído como aplicativo/plataforma completa?
14. O que é Lunet Store?
15. Qual a diferença entre Store e Library?
16. Assets, Plugins, Templates, Packages e Tools podem fazer parte do catálogo?
17. Como capabilities externas aparecem dentro de um Product Shell?
18. O que significa Connections?
19. Qual é a estratégia first-party?
20. Por que a migração não deve executar a arquitetura futura imediatamente?

Nenhuma dessas respostas deve depender desta conversa depois que a tarefa terminar.

---

# 23. Verificação

Após as alterações:

1. rode todos os checks existentes;
2. atualize checks apenas quando a nova regra puder ser fiscalizada de forma confiável;
3. rode self-tests;
4. confirme que nenhuma invariante anterior foi enfraquecida;
5. confirme que nenhum código de Lunet2D ou Urbe foi importado;
6. confirme que nenhum diretório vazio foi criado;
7. confirme que P1-1/P1-2 agora capturam o novo mapa funcional;
8. produza handoff com:
   - arquivos alterados;
   - decisões persistidas;
   - ADRs criados;
   - invariantes afetadas;
   - verificações;
   - próximos passos;
   - decisões do proprietário ainda pendentes.

Faça commit segundo o processo vigente do repositório.

Não declare a migração iniciada.

O resultado desta tarefa é o **alinhamento arquitetural que precede a migração**.
