# Arquitetura, distribuição e plataforma própria

> **Autoridade:** documento de arquitetura, subordinado ao `MANIFEST.md` (em especial **NN-023**), à decisão do proprietário [ADD-0002](../governance/addenda/ADD-0002-product-shells-distribuicao-independente.md) (DEC-0006) e ao [ADR-0006](../adr/0006-product-shell-context-distribuicao-independente.md).
> **Natureza:** conceitual. **Nada aqui implementa** Distribution Profiles, Store, Library, marketplace, pagamentos, contas, servidores, identidade, catálogo ou qualquer Service. Nenhum schema definitivo é criado.
> Legenda: **[Decisão]** tomado pelo proprietário. **[Requisito]** decorre diretamente do texto do proprietário. **[Derivado]** dedução do agente a partir do texto, sujeita a ADR (MANIFEST §53: não é decisão). **[Aberto]** sem decisão.

## 1. Arquitetura não é distribuição [Decisão]

```text
o componente existe no Ecosystem        ≠        o componente é distribuído a um usuário
```

Uma Tool, Service, Workspace ou Product existir no monorepo ou no Registry **não** significa automaticamente que está: incluído em todo produto · instalado · visível · disponível publicamente · gratuito · adquirível · utilizável naquele Host.

## 2. Distribution Profile [Decisão: conceito · Aberto: nome e formato]

Mecanismo conceitual (o nome definitivo pode ser refinado por ADR) que descreve **quais componentes formam determinada edição/distribuição**. Exemplo conceitual (não é formato):

```text
lunet-public
  Product:     Lunet2D
  Bundled:     Lunet Product Shell · Lunet Framework · Runtime · Compiler · Project System · funcionalidades essenciais
  Optional:    Agent Workspace · ferramentas adicionais · plugins · packages
  Marketplace: assets · templates · plugins · packages · Tools compatíveis
  Internal:    administração geral do Ecosystem · observabilidade cross-product do proprietário · ferramentas privadas
```

Não implementado. Plano: formato e validação na **Fase 2** (ADR), packaging/distribuição na **Fase 4**.

Consequências para a arquitetura [Requisito]:

- o estado "distribuído/instalado/visível" de um componente é um dado **do perfil e do usuário**, não do componente;
- **Connections** (UX, ver [`product-model.md`](product-model.md) §5) mostra o que o perfil inclui e o que está instalado, consultando o Registry — sem modelo paralelo;
- uma capability opcional ausente nunca vira dependência essencial (MANIFEST §7, §9).

## 3. Três eixos independentes de disponibilidade [Requisito]

A arquitetura precisa suportar, **separadamente**:

| Eixo | Valores conceituais (não são contrato) |
|------|----------------------------------------|
| 1. Visibilidade | `public` · `private` · `internal` |
| 2. Forma de distribuição | `bundled` · `optional` · `marketplace` · `unavailable` |
| 3. Modelo comercial | `free` · `paid` · `subscription` · `entitlement` |

**[Aberto]** Nomes e formato concretos só serão definidos na fase de Contracts/Registry/Packaging, por ADR. Não foi adicionado nenhum campo a `ecosystem.json` nem a schemas por causa disto.

## 4. Independência de distribuição: NN-023 [Decisão]

> Um Product declarado distribuível deve poder ser empacotado, publicado, instalado, atualizado e utilizado em seu domínio essencial sem exigir que o Ecosystem Hub seja distribuído ao usuário final.

Complementa NN-003 (funcionamento) com a garantia de **distribuição**. Cenário suportado explicitamente: `Ecosystem Hub: visibility private/internal` com `Lunet2D` e `Urbe` públicos. Fiscalização: [`enforcement-matrix.json`](../governance/enforcement-matrix.json) (hoje `CHK-BOUNDARIES` sobre o grafo declarado; testes standalone, release pipeline, packaging, validação de Distribution Profile e DEVICE em fases posteriores).

## 5. Store e Library são conceitos diferentes [Decisão]

```text
Store    = conteúdo disponível para adquirir/instalar
Library  = conteúdo que o usuário possui, instalou ou tem direito de usar
```

A Library do Lunet poderá conter: projetos, assets, plugins, templates, packages, Tools, conteúdo adquirido, downloads, atualizações. **Não implementado.**

## 6. Um catálogo, não duas infraestruturas [Decisão]

O Lunet poderá ter um ecossistema comercial próprio. Evitar construir duas infraestruturas separadas para `Store` e `Asset Store`:

```text
Lunet Store
├── Assets
├── Plugins
├── Templates
├── Packages
└── Tools
```

Na UX pode existir uma área "Asset Store"; tecnicamente, **Assets são uma categoria do catálogo/marketplace geral do Lunet**. Nenhum backend, marketplace ou sistema comercial é implementado agora.

## 7. Plataforma first-party própria [Decisão]

O Ecosystem deve permitir que o proprietário possua sua própria plataforma de: distribuição · downloads · atualizações · catálogo · identidade · biblioteca · entitlements/licenças · commerce · marketplace · comunidade.

> **First-party by default; external distribution by choice.**

Plataformas externas não são a autoridade arquitetural do produto. Podem, no futuro, receber uma versão Starter, de divulgação, uma edição limitada ou um canal de aquisição, enquanto a versão completa é distribuída pela infraestrutura própria.

**Requisitos decorrentes** [Derivado — sujeitos a ADR; não são decisão]:

1. **Canal ≠ produto.** O artefato de um Product é produzido uma vez pelo release pipeline e publicado em canais diferentes; nenhuma plataforma de distribuição entra no domínio essencial do Product. Uma estratégia comercial atual não pode virar dependência técnica impossível de mudar (texto do proprietário).
2. **Edições por Distribution Profile,** não por bifurcação de código: Starter/limitada/completa são perfis sobre os mesmos componentes.
3. **Identidade e entitlements não entram no domínio essencial.** Conta/licença só é exigida para funcionalidades de catálogo, commerce e comunidade. Projetos, vaults e arquivos do usuário continuam utilizáveis sem conta, sem servidor e offline (MANIFEST §40, §41).
4. **Nenhum backend obrigatório** para o domínio essencial (MANIFEST §40, §47).
5. **Autoridade única** para catálogo, entitlements e versões publicadas (NN-001): quando existirem, cada um terá uma fonte canônica; caches e índices (inclusive os do Hub ou do portal) permanecem projeções (MANIFEST §42).

## 8. Infraestrutura compartilhada, experiência própria [Decisão]

No futuro podem existir Services compartilhados: `Identity`, `Catalog`, `Commerce`, `Entitlements`, `Downloads`, `Updates`, `Reviews`, `Creator Profiles`, `Notifications`. Cada Product Shell apresenta **experiência própria**: a "Lunet Store" pode usar Catalog, Commerce e Entitlements sem o usuário conhecer esses nomes; uma futura "Urbe Store" poderia reutilizar a infraestrutura sem compartilhar catálogo, política ou identidade visual.

**Nenhum desses Services deve ser criado agora.** NN-020 e NN-022 valem integralmente: extração só acontece quando reduz a complexidade total e existem consumidores reais, contrato e ADR. Nomes citados no texto do proprietário como caminhos de exemplo (`platform/commerce`, `platform/identity`, `platform/store`, `platform/community`, `platform/entitlements`, `platform/distribution`, `tools/editor`, `tools/sprite-studio`, `workspaces/agent`) **não existem e não devem ser criados** sem implementação real.

## 9. O que ainda não está decidido [Aberto]

- nome, formato e validação do Distribution Profile;
- nomes e contrato dos três eixos de disponibilidade;
- quais Services existirão, com que fronteiras, e se algum será extraído;
- qual é a fonte canônica de catálogo, entitlements e identidade;
- política de edições (Starter/limitada/completa) e canais externos;
- modelo de extensões/plugins e de Tools de terceiros no catálogo (MANIFEST §29 exige ADR para o modelo de plugins).
