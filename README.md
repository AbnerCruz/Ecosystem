# Ecosystem

Plataforma modular para desenvolver, executar, observar, combinar e evoluir aplicativos, ferramentas, workspaces, agentes e serviços sob uma arquitetura comum — preservando a independência de cada produto.

Produtos principais iniciais:

| ID | Produto | Estado no monorepo |
|----|---------|--------------------|
| `lunet2d` | **Lunet2D** — desenvolvimento de jogos 2D (framework, runtime, tooling) | não migrado |
| `urbe` | **Urbe** — workspace de conhecimento, documentos e organização | não migrado |
| `hub` | **Ecosystem Hub** — Control Plane, Host geral, Registry e Launcher | planejado |

A fonte de verdade desta tabela é [`ecosystem.json`](ecosystem.json); este README é apenas navegação.

## Portal

**https://abnercruz.github.io/Ecosystem/** — portal humano de desenvolvimento, distribuição, documentação, testes e recuperação (GitHub Pages). É uma projeção gerada das fontes canônicas, não fonte de verdade, e não é o Ecosystem Hub. Veja [`docs/architecture/portal.md`](docs/architecture/portal.md).

## Estado atual

**Fase 0 — Constituição.** O repositório contém a fundação documental e os checks de consistência. Nenhum produto foi importado ainda. Veja [`ROADMAP.md`](ROADMAP.md) para a próxima tarefa e o motivo.

## Por onde começar

| Documento | Papel |
|-----------|-------|
| [`MANIFEST.md`](MANIFEST.md) | **Constituição normativa.** Prevalece sobre todo o resto. |
| [`AGENTS.md`](AGENTS.md) | Protocolo operacional obrigatório para agentes, com as invariantes `NN-XXX`. |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Conceitos, boundaries, autoridades e o que ainda **não** foi decidido. |
| [`ROADMAP.md`](ROADMAP.md) | Fases, gates, tarefas e próxima ação. |
| [`ecosystem.json`](ecosystem.json) | Mapa canônico dos componentes, legível por máquina. |
| [`docs/adr/`](docs/adr/) | Decisões arquiteturais. |
| [`docs/governance/`](docs/governance/) | Comunicação humano↔máquina e máquina↔máquina, Definition of Done, matriz de enforcement, decisões pendentes, handoffs. |
| [`docs/contracts/`](docs/contracts/) | Contratos e schemas. |
| [`docs/migration/`](docs/migration/) | Estratégia de migração de Lunet2D e Urbe. |
| [`docs/architecture/portal.md`](docs/architecture/portal.md) | Portal web (GitHub Pages): papel, projeção, publicação. |
| [`docs/architecture/product-model.md`](docs/architecture/product-model.md) · [`distribution.md`](docs/architecture/distribution.md) · [`product-vision.md`](docs/architecture/product-vision.md) · [`faq.md`](docs/architecture/faq.md) | Product Shell, Context, distribuição independente, plataforma first-party, visão de Lunet2D/Urbe e perguntas-chave (ADD-0002). |
| [`site/`](site/) | Código do portal web. |

## Verificação

```bash
dotnet run tests/consistency/Check.cs                 # checks de consistência (.NET SDK 10+)
dotnet run tests/consistency/Check.cs -- --self-test  # prova que cada check detecta sua violação
```

Os mesmos comandos rodam no CI ([`.github/workflows/consistency.yml`](.github/workflows/consistency.yml)).
