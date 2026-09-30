# Modelo de produto: níveis, Product Shell, Context e Connections

> **Autoridade:** documento de arquitetura, subordinado ao `MANIFEST.md`, à decisão do proprietário [ADD-0002](../governance/addenda/ADD-0002-product-shells-distribuicao-independente.md) (DEC-0006) e ao [ADR-0006](../adr/0006-product-shell-context-distribuicao-independente.md).
> **Natureza:** conceitual. Este documento **não cria código, diretórios, contratos nem componentes**. Onde algo é "futuro", a fase está indicada e o formato concreto depende de ADR.
> Legenda: **[Decisão]** foi tomado pelo proprietário (ADD-0002). **[Requisito]** decorre diretamente da decisão. **[Aberto]** continua sem decisão.

## 1. Três níveis de experiência [Decisão]

```text
LEVEL 1   ECOSYSTEM HUB      universo geral do proprietário/ecossistema
    ↓
LEVEL 2   PRODUCT SHELL      universo especializado de um produto
    ↓
LEVEL 3   WORKSPACE / TOOL   atividade concreta do usuário
```

Caminhos válidos:

```text
Ecosystem → Lunet2D → MeuJogo → Editor
Ecosystem → Lunet2D → MeuJogo → Sprite Studio
Ecosystem → Urbe    → Meu Vault → Editor
Ecosystem → Sprite Studio          (Tool com capacidade de funcionar standalone)
```

O último caminho mostra que o Product Shell **não** torna as Tools reutilizáveis propriedade privada do produto (MANIFEST §3).

## 2. Product Shell [Decisão]

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
- não deve fazer Tools reutilizáveis conhecerem o Host concreto (NN-007).

Exemplos iniciais: *Lunet Product Shell* e *Urbe Product Shell*.

**Relação com as definições do MANIFEST.** Um Product Shell é o **Host** do seu Product (MANIFEST §6.2: hoje "Lunet2D" aparece como exemplo de Host). Não é um tipo novo de componente no `ecosystem.json`: é um papel arquitetural do Product. Nenhum Shell foi criado nem declarado; isso é trabalho de fases posteriores e depende do que os inventários (P1-1/P1-2) revelarem sobre o código real.

**Product Shell é estrutura arquitetural comum, não UX igual.** Lunet e Urbe preservam domínio, identidade, navegação, modelo mental, dados e experiência. Nenhum dos dois vira "skin" do Ecosystem.

## 3. Hub e Product Shell não são a mesma coisa [Decisão]

| | Ecosystem Hub | Product Shell |
|--|---------------|---------------|
| Escopo | transversal: o ecossistema inteiro | um Product |
| Papéis | Control Plane, Host geral, Launcher, Registry, observação do ecossistema, ambiente do proprietário, possível ponto de entrada para produtos, Tools e Workspaces | representar o domínio do seu Product e hospedar Tools/Workspaces nesse domínio |
| Necessário para distribuir o Product? | **não** (NN-003, NN-023) | é parte do Product |

Cenário válido [Decisão]:

```text
INTERNO / PROPRIETÁRIO                       PÚBLICO
Ecosystem Hub (visibility: private/internal)  Lunet2D  (visibility: public)
├── Lunet2D, Urbe                             Urbe     (visibility: public)
├── Tools, Workspaces
├── agentes, observabilidade, administração
```

O proprietário pode nunca distribuir o Hub publicamente. Isso não pode impedir a distribuição completa de nenhum Product (**NN-023**). Detalhes de distribuição: [`distribution.md`](distribution.md).

## 4. Context [Decisão: conceito · Aberto: contrato]

Context representa o **escopo atual** no qual uma operação, Tool, Workspace ou Agent está trabalhando. É hierárquico:

```text
Ecosystem                                  (Ecosystem Context)
Ecosystem → Lunet2D
Ecosystem → Lunet2D → MeuJogo
Ecosystem → Lunet2D → MeuJogo → Editor
Ecosystem → Urbe → MeuVault → Nota X
```

**Requisitos futuros** [Requisito]: o conceito precisa permitir que

- Tools recebam contexto;
- Agents recebam contexto;
- permissões dependam do contexto (NN-016);
- capability discovery seja contextual;
- a UI possa priorizar capacidades relevantes;
- operações apontem precisamente para Product/Project/Vault/documento/asset atual.

**[Aberto]** O contrato concreto (estrutura, serialização, herança entre níveis, relação com o envelope do protocolo runtime de MANIFEST §14 e com `Agent Workspace` de MANIFEST §20) **não** foi definido e pertence às fases de Contracts/Registry (Fase 2) e Runtime (Fase 5). Este documento não deve ser usado como especificação do contrato.

## 5. Connections é UX; Capabilities é arquitetura [Decisão]

Product Shells podem ter uma superfície chamada **Connections**, por exemplo:

```text
Lunet2D → Connections
  Editor · Sprite Studio · Agent Workspace · GitHub · Audio Studio · ...
```

```text
Connections  = experiência de usuário
Capabilities = mecanismo arquitetural
```

`Connection` **não** é uma arquitetura concorrente a Capabilities. Por baixo, a experiência deve ser implementável futuramente por: Capability Registry, discovery, compatibility, permissions e estado de instalação/distribuição ([`distribution.md`](distribution.md) §2). Não existe, e não deve existir, um segundo registro, protocolo ou modelo de dados de "conexões" (NN-001, NN-006).

## 6. Mapa de fases

| Conceito | Onde passa a ser concreto |
|----------|---------------------------|
| Inventário do que hoje seria Product Core / Shell / Tool / Workspace / Service / Library / Adapter | Fase 1 (P1-1, P1-2), **como proposta**, sem extração |
| Contrato de Context e metadados de disponibilidade | Fase 2 (ADR) |
| Host API de Product Shell, Connections sobre o Registry | Fase 5 |
| Distribution Profile | Fase 2 (formato, ADR) e Fase 4 (packaging/distribuição) |

O ROADMAP é a autoridade das tarefas e fases.
