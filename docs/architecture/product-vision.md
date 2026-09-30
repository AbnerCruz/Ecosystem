# Visão de produto: Lunet2D e Urbe como Products completos

> **Autoridade:** visão de produto registrada pelo proprietário em [ADD-0002](../governance/addenda/ADD-0002-product-shells-distribuicao-independente.md) (DEC-0006). Abaixo do `MANIFEST.md` e de qualquer SPEC ou ROADMAP do próprio produto.
> **Isto é visão, não estado.** Nada aqui está implementado. **Não presuma que os módulos listados existem.** **Não altere o Lunet2D nem o Urbe atuais para imitar esta estrutura.** O código real só será conhecido nos inventários P1-1/P1-2 e este documento não deve ser usado para afirmar o que os produtos contêm hoje.

## 1. Lunet2D

**Hoje** (descrição do proprietário): ambiente de desenvolvimento de jogos 2D em C#, com framework, runtime, IDE/tooling e outras capacidades. A fonte de verdade do estado real é o repositório de origem até a migração (`ecosystem.json`).

**Visão futura** [Decisão]:

> Lunet2D é uma plataforma completa de criação, desenvolvimento, execução, extensão, distribuição e comunidade para jogos 2D em C#, integrada ao Ecosystem, mas distribuível e utilizável de forma independente.

```text
LUNET2D PLATFORM

Core                       Product Shell                 Catalog
├── Lunet Framework        ├── Home                      ├── Assets
├── Compiler               ├── Projects                  ├── Plugins
├── Runtime                ├── Library                   ├── Templates
├── Project System         ├── Store                     ├── Packages
└── Build                  ├── Community                 └── Tools
                           ├── Learn
Reusable capabilities      ├── Games
├── Editor                 └── Connections
├── Sprite Studio
├── Tilemap Studio
├── Agent Workspace
├── GitHub Workspace
└── outras capabilities
```

Relações com o restante da arquitetura:

- `Store` e `Library` são conceitos distintos; `Asset Store` é UX sobre uma categoria do catálogo geral ([`distribution.md`](distribution.md) §5–§6).
- `Connections` é UX sobre o Capability Registry ([`product-model.md`](product-model.md) §5).
- Editor, Sprite Studio, Tilemap Studio, Agent Workspace e GitHub Workspace são capabilities **reutilizáveis**: o Lunet as hospeda, não as possui (MANIFEST §3, NN-007).
- O Lunet continua utilizável sem o Hub e sem capabilities opcionais (NN-003, MANIFEST §7) e distribuível sem o Hub (NN-023).
- O Lunet continua um produto especializado em jogos 2D (MANIFEST §5.1): "plataforma" amplia o ciclo de vida do produto (criação → distribuição → comunidade), não o transforma em monólito nem em "superaplicativo" (MANIFEST §2).

## 2. Urbe

O Urbe também é tratável como Product completo, com seu próprio Product Shell. Estrutura conceitual [Decisão]:

```text
URBE PRODUCT SHELL
Home · Vault · World · Notes · Pages · Library · Extensions · Templates · Connections
```

Isto **não** significa copiar a UI do Lunet: Product Shell é estrutura arquitetural comum, não UX igual. O Urbe preserva domínio, identidade, navegação, modelo mental, dados e experiência, e continua em JavaScript (NN-005): qualquer integração acontece por adapters. Uma futura "Urbe Store" poderia reutilizar a infraestrutura compartilhada sem compartilhar catálogo, política ou identidade visual ([`distribution.md`](distribution.md) §8).

## 3. O que este documento não autoriza

Separar o Editor; extrair o Sprite Studio; reescrever o Agent Workspace; criar Store, Library ou Product Shell novo; transformar código em capabilities; reorganizar em packages; mudar a arquitetura interna dos produtos porque a arquitetura futura já é conhecida. A migração preserva o que existe (NN-013, [`docs/migration/README.md`](../migration/README.md) §7).
