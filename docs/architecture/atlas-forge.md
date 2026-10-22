# Atlas Forge — aplicação independente no Ecosystem

Autoridades: `ecosystem.json` (componente/versão/comandos), [ADR-0035](../adr/0035-atlas-forge-incorporacao-gradual.md), [DEC-0054 / ADD-0031](../governance/addenda/ADD-0031-atlas-forge-no-ecosystem.md), [SPEC](../../apps/atlas-forge/SPEC.md) e [roadmap](../../apps/atlas-forge/ROADMAP.md).

## Classificação e boundary

`atlas-forge` é um **Product independente**, código canônico em `apps/atlas-forge`. Tem valor de ferramenta de autoria para o Ecosystem, mas ainda não é uma Tool global compartilhada ou capability pública: não há segundo Host concreto integrado. Essa distinção segue NN-007/020/022 e não diminui o valor de uso standalone.

O stack HTML/CSS/JS modular e Canvas foi solicitado no mandato e ratificado na incorporação. É uma exceção local, sem alteração do padrão C# da infraestrutura compartilhada. O editor não depende de Hub, Urbe ou Lunet; não importa código de outro Product, não modifica vault e não reintroduz simulação no Urbe nativo.

## Layout e dados

- `src/core`, `geometry`, `generation`, `solvers`: definição, estado e cálculos locais.
- `src/app`: composição do editor e Worker; `rendering`: projeções/cache/Canvas.
- `storage`, `documents`: projetos versionados, cópia local verificada e snapshots de notas.
- `publishing`: projeção HTML imutável cujo bundle não inclui solvers/editor/storage.
- `scripts`, `tests`, `fixtures`: build autossuficiente, exemplo, benchmarks e corpus sintético.

A mudança de localização preserva histórico Git e runtime, schema v2/algoritmo, IndexedDB, arquivos importados e comportamento. A identidade do pacote de desenvolvimento passa de `atlas-forge-proposal` a `atlas-forge`; isso não é migração de dados do usuário nem mudança de chave de storage. Evidências iniciais ficam em `docs/proposals/atlas-forge` como registros históricos; não há cópia editável concorrente.

## CI, integração e distribuição

Workflow seletivo e reutilizável: `.github/workflows/atlas-forge-checks.yml`, Ubuntu 24.04 suportado pelo Playwright fixado. Executa formatação, núcleo, build, browser/offline e benchmark. É chamado pelo integrador no estado combinado; falha do CI do aplicativo impede integração, preservando a aprovação crítica exigida pela política.

O build produz editor offline, JSON de exemplo, Markdown e viewer independente. GitHub Actions disponibiliza esses artefatos para revisão. Não foi criado canal público de produção nem tag/release. Perfil alvo em `docs/distribution/atlas-forge.target.profile.json` declara ausência de distribuição operacional; `current.profile.json` não recebe um canal fictício. Uso e empacotamento do HTML não dependem da distribuição do Hub.

## Limites preservados

A aprovação de incorporação não valida ciência, Android ou corpus Alpha real. Relevo/clima/biomas heurísticos, drenagem simplificada, mapas locais esquemáticos e marcos de estado continuam explicitamente classificados. Tectônica, ecologia, civilizações e eras causais seguem no roadmap. A integração na main é um fato observado somente depois de o integrador registrá-la.
