# ADR-0012 — Contratos da Fase 2: ComponentManifest, Capability, versões, Context, permissões, Registry e Distribution Profile

## Status

Proposto — a ser ratificado pelo proprietário em DEC-0020 (não bloqueante: o vertical slice e os checks já funcionam com estes contratos). Aplica o princípio local-first ([ADR-0011](0011-local-first-e-promocao-por-evidencia.md)) à própria plataforma. Os **nomes e valores dos eixos do Distribution Profile são provisórios** até a ratificação.

## Contexto

O MANIFEST (§6, §10, NN-006, NN-007, NN-016, NN-023) e o ROADMAP exigem, na Fase 2, contratos explícitos antes de qualquer extração: manifest de componente, capability versionada com provider/consumer, compatibilidade, Context, modelo inicial de permissões, Registry e Distribution Profile. Nada disso existia como contrato validável. O gate da fase: "um componente pode declarar uma capability, outro pode descobri-la e a compatibilidade pode ser validada sem dependência direta entre produtos".

## Problema

Qual é o **mínimo** contrato que torna capabilities declaráveis, descobríveis e validáveis, sem criar uma segunda fonte para o mapa de componentes (NN-001), sem package manager nem runtime (Fase 5) e sem criar diretórios ou componentes compartilhados sem consumidor real (NN-020, NN-022, ADR-0004)?

## Opções

1. **Estender o que já é canônico:** o ComponentManifest **é** a entrada de componente de `ecosystem.json` (já com ID estável, tipo, owner, versão, path, dependências), ganhando `provides`, `requires` e `permissions`; contratos de capability em arquivos próprios; Registry derivado, dentro dos checks.
2. **Manifest por componente** em arquivos separados (`component.json` em cada diretório) mais um agregador.
3. **Registry como serviço/projeto separado desde já** (`platform/registry`).

## Decisão

Opção 1 (a ratificar em DEC-0020).

- **ComponentManifest = entrada de `ecosystem.json`** (`ecosystem.schema.json#/$defs/component`). Identidade: a **chave** (ID estável, NN-019); `type`, `version` (autoridade), `owners`, `status`, `dependencies`, mais `responsibility`/`contract`/`consumers`/`compatibility`/`extractionReason` para compartilhados (NN-004). Acrescentados, opcionais: `provides` (`{capability, version}`), `requires` (`{capability, range, optional?}`) e `permissions.requests`. **Nada de estado de execução**: o manifest é estrutura. Campos de distribuição (visibility/distribution/commercial) **não** entram no manifest: pertencem ao Distribution Profile.
- **Capability = contrato versionado** em `docs/contracts/capabilities/<id>.json` (`capability-contract.schema.json`): ID (`sprite.edit`), `versions[]` com `inputs`, `outputs`, `errors`, `requiredPermissions`, `lifecycle` e `status`, e `compatibility: semver`. O **provider não é declarado no contrato**: é derivado dos `provides` dos manifests (uma autoridade). Um consumidor depende da capability e da faixa, nunca de classe, Product, Host ou path (NN-006, NN-007).
- **Versionamento e compatibilidade (mínimo):** `MAJOR.MINOR.PATCH` sem pré-lançamento; mudança incompatível incrementa MAJOR. Faixas: exata (`1.2.0`), caret (`^1.2.0`; em `0.x` o MINOR é quebra), tilde (`~1.2.0`) e comparadores (`>=1.0.0 <2.0.0`). Um provider **satisfaz** um consumer quando sua versão está na faixa. Sem resolução de grafo, sem lockfile, sem pré-release.
- **Permissões (modelo inicial):** catálogo `docs/contracts/permissions.json` (ids `fs.read`, `fs.write`, `network.access`, `execute.code`, `agent.act`, `ui.display`; `sensitive`). Deny-by-default: o componente só tem o que **solicitou** (`permissions.requests`), um pedido fora do catálogo é inválido, e um consumidor precisa ter solicitado as `requiredPermissions` do contrato da versão que consome. Um agente nunca herda `sensitive` por conveniência (NN-016). Enforcement em runtime: Fase 5.
- **Context** (`context.schema.json` + regras): caminho do mais amplo ao mais específico — `ecosystem → product → project → workspace → tool`; começa em `ecosystem`, níveis em ordem estrita sem repetir, `product` referencia um componente `product`. Base para Agents, Tools, permissões e discovery contextual; **não** é IPC nem Host API (Fase 5).
- **Registry inicial:** índice local/estático **derivado** dos manifests e contratos: registrar, indexar capabilities, descobrir providers (`Discover`) e validar compatibilidade (`Validate`), com códigos de erro estáveis (`CAP_UNKNOWN`, `CAP_VERSION_UNKNOWN`, `CAP_NO_PROVIDER`, `CAP_INCOMPATIBLE`, `RANGE_INVALID`, `PERMISSION_UNKNOWN`, `PERMISSION_MISSING`, `TOOL_KNOWS_HOST`, `PRODUCT_DEPENDS_ON_PRODUCT`). Mora **dentro dos checks** (`tests/consistency/Check.cs`: `CHK-REGISTRY` e `-- --registry [--discover ...]`), onde estão seus consumidores reais. Promoção a componente próprio só quando o Hub (ou outro consumidor real) existir (ADR-0011). Sem GitHub como runtime, sem serviço remoto.
- **Distribution Profile** (`distribution-profile.schema.json` + regras): lista de entradas `{component, availability (bundled|optional|marketplace|unavailable), visibility (public|unlisted|private|internal), distribution (first-party|external-store|none), commercialModel (free|paid|subscription|undecided)}`. Os três eixos são independentes; **nomes e valores provisórios (DEC-0020)**. NN-023: um perfil que inclui componente público não pode ter o Hub `bundled`.
- **Vertical slice** em `docs/contracts/examples/registry-slice/`: capability de teste `capability.test`; provider A v1, consumer B `^1.0.0` (descoberta e compatibilidade OK) e consumer C `^2.0.0` (falha `CAP_INCOMPATIBLE`), mais negativos de permissão, capability desconhecida, sem provider, Tool que conhece Host e Product→Product. Fixtures, **sem extrair nenhuma Tool real**.
- **Nenhum diretório novo** (`platform/`, `tools/`…): arquivos de contrato ficam em `docs/contracts/`; nada é criado sem conteúdo.

## Consequências

- O gate da Fase 2 tem evidência executável (`CHK-REGISTRY` + self-test), não só documentos.
- Nenhum Product muda: `ecosystem.json` real não declara capabilities hoje (nenhum consumidor real); `provides`/`requires` passam a ser usados quando a primeira capability for **promovida por evidência** (ADR-0011).
- O Registry é pequeno e privado aos checks; o custo de promovê-lo depois é conhecido (extrair `Registry`, `SemVer`, `VersionRange`).
- Os nomes dos eixos podem mudar na ratificação sem afetar manifests (o perfil é um arquivo separado).

## Alternativas rejeitadas

- **Manifest por componente (opção 2):** duas fontes para o mapa de componentes (NN-001) e arquivos dentro de `apps/<id>` (NN-013).
- **Registry separado já (opção 3):** infraestrutura sem consumidor além dos checks (NN-020, NN-022).
- **Package manager / resolução de grafo / pré-release:** fora do mínimo e sem consumidor.

## Referências

MANIFEST §6, §10, §29; NN-001, NN-004, NN-006, NN-007, NN-016, NN-019, NN-020, NN-022, NN-023; ADD-0002 §6–§8; ADR-0004, ADR-0009, ADR-0011; DEC-0020; [`docs/contracts/README.md`](../contracts/README.md); [`product-model.md`](../architecture/product-model.md) §4.
