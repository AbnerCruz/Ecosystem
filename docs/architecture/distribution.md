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

- nomes **finais** dos eixos do Distribution Profile (o formato e a validação existem desde a Fase 2, ADR-0012; os nomes seguem provisórios, DEC-0020-B, até a decisão de distribuição DEC-0021);
- a distribuição **definitiva** dos Products (DEC-0021; ver §10);
- quais Services existirão, com que fronteiras, e se algum será extraído;
- qual é a fonte canônica de catálogo, entitlements e identidade;
- política de edições (Starter/limitada/completa) e canais externos;
- modelo de extensões/plugins e de Tools de terceiros no catálogo (MANIFEST §29 exige ADR para o modelo de plugins).

## 10. Source × Distribution e o arranjo atual (P2-12) [Fato · Aberto: destino]

```text
SOURCE        = onde o produto é DESENVOLVIDO            → Ecosystem, apps/<id>   (ecosystem.json: components.<id>.path)
DISTRIBUTION  = por onde builds/releases/web CHEGAM      → hoje, os repositórios de origem        (docs/distribution/current.profile.json)
                ao usuário
```

**Fato (DEC-0008-A, DEC-0009-A, DEC-0016, DEC-0017-A; transitório e intencional):** o código vive **só** no Ecosystem (`apps/urbe/`, `apps/lunet2d/`). `AbnerCruz/Urbe` e `AbnerCruz/Lunet2D` **não são lugares de desenvolvimento**: são espelhos que o `sync-from-ecosystem.yml` mantém iguais a `apps/<id>` e que continuam construindo e publicando releases (e o Urbe Web, no Pages) com os pipelines e secrets que já tinham. Um commit humano que mexa fora de `.github/` nesses repositórios é deriva (o espelho para e abre uma Issue). Observação: o campo `source.repository` de `ecosystem.json` guarda esse repositório de **origem** (nome histórico); não é a fonte de desenvolvimento, que é o `path`.

O arranjo atual está descrito **como dado**, sem alterar nenhum canal, em [`docs/distribution/current.profile.json`](../distribution/current.profile.json) (Distribution Profile `current`, validado por `CHK-REGISTRY`; as localizações vêm de `ecosystem.json`, não são copiadas). Achado de P2-12: o eixo provisório `distribution` não tinha um valor para "canal externo ao Ecosystem" (GitHub Releases/Pages); foi acrescentado `external-channel` — mais uma razão para os nomes só serem congelados depois da decisão (P2-9).

### Consequências reais por cenário (insumo de DEC-0021)

| Aspecto | A — manter por mais tempo | B — centralizar no Ecosystem/GitHub | C — plataforma first-party (progressivo) |
|---------|---------------------------|--------------------------------------|-------------------------------------------|
| Apps instalados | nada muda | Urbe só atualiza se a versão-ponte sair antes no canal antigo (mudança de produto, item próprio, NN-013); beta: reinstalar é aceitável (ADD-0006) | só quando a plataforma existir; precisa de versão-ponte por produto |
| URLs existentes | iguais | Urbe Web muda de `/Urbe/` para uma rota do Ecosystem; links divulgados quebram sem redirecionamento | iguais até a migração de cada produto; depois, redirecionamento a partir dos repositórios antigos |
| Canais de atualização | `releases/latest` por produto, como hoje | `releases/latest` passa a ser único para o repositório (tags prefixadas): os leitores atuais podem ler a release de outro produto | canal novo por produto; os antigos permanecem como ponte |
| Assinatura Android | chave e `versionCode` ficam onde estão (R-LUN-1/R-URB-1 eliminadas) | a chave e os secrets precisam ser levados ao Ecosystem; sem a mesma chave o APK não atualiza por cima | idem B para a plataforma que assinar/hospedar |
| Electron updater (Urbe) | lê `owner/repo` do `package.json`, sem mudança | exige mudar o repositório lido (código de produto) | exige canal compatível ou ponte |
| Urbe Web / PWA | mesmo escopo de service worker e PWAs instalados | escopo e URL do service worker mudam; PWAs instalados e o armazenamento por origem precisam ser provados antes (P1-10 já mostrou o cuidado) | depende do domínio da plataforma; mesma ressalva |
| GitHub Releases | continuam sendo o histórico e a fonte dos instaladores | histórico antigo fica no repositório de origem; novas no Ecosystem | passam a canal alternativo/espelho |
| Histórico, Issues e PRs | preservados (DEC-0013-A: Issues dos produtos ficam nas origens) | idem; nada é apagado | idem; nada é apagado |
| Redirects | desnecessários | necessários (Pages e links) | necessários na migração |
| Fallback / recuperação | o proprietário já instala a partir das releases (portal) | manter o canal antigo vivo durante a transição | manter os canais antigos como recuperação |
| Custo | latência de 10–15 min do espelho; dois repositórios vivos | mudança de produto e risco de quebra | construir a plataforma (Services especulativos são proibidos sem consumidor, NN-020/NN-022) |

**Invariante de todos os cenários:** nenhum repositório antigo é apagado ou arquivado, nenhum Pages é desligado e nenhuma release é removida por esta decisão; qualquer aposentadoria é item próprio, com plano de migração e evidência. A direção de longo prazo (Ecosystem como fonte; plataforma first-party como possível distribuição principal; repositórios antigos como espelho/canal alternativo/legado) é a de [§7](#7-plataforma-first-party-própria-decisão), sem prazo.
