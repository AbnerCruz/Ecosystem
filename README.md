# Ecosystem

Plataforma modular para desenvolver, executar, observar, combinar e evoluir aplicativos, ferramentas, workspaces, agentes e serviços sob uma arquitetura comum — preservando a independência de cada produto.

Produtos principais: **Lunet2D** (desenvolvimento de jogos 2D: framework, runtime e tooling), **Urbe** (workspace de conhecimento, documentos e organização) e o **Ecosystem Hub** (Control Plane, Host geral, Registry e Launcher). Cada um mantém lifecycle e versão próprios.

> **Estado vivo não mora aqui.** Este README é navegação e explicação estável: ele **não** declara fase, status de componente nem andamento. Fontes (uma por pergunta):
>
> | Pergunta | Autoridade |
> |----------|-----------|
> | Quais componentes existem, onde vivem e em que estado (`status`) | [`ecosystem.json`](ecosystem.json) |
> | Qual fase, qual gate está aprovado, qual é a próxima tarefa | [`ROADMAP.md`](ROADMAP.md) |
> | Qual tarefa está em andamento | Issues do GitHub (`state:<estado>`) |
> | O que terminou e com que evidência | [`docs/governance/handoffs/`](docs/governance/handoffs/) |
> | O que depende do proprietário (decisões e validações pendentes) | o portal, derivado de `decisions.json` e dos handoffs |
> | Versão, releases e validação de cada build | o portal, derivado das releases e de [`docs/validation/`](docs/validation/) |
>
> Para ver tudo isso de uma vez: o portal (abaixo).

## Portal

**https://abnercruz.github.io/Ecosystem/** — portal humano de desenvolvimento, distribuição, documentação, testes e recuperação (GitHub Pages). É uma projeção gerada das fontes canônicas, não fonte de verdade, e não é o Ecosystem Hub. Veja [`docs/architecture/portal.md`](docs/architecture/portal.md).

## Onde estão os produtos

O código de Lunet2D e Urbe vive em `apps/lunet2d/` e `apps/urbe/` deste repositório, com o histórico preservado (`ecosystem.json` é a fonte do `status` de cada um). Os repositórios `AbnerCruz/Lunet2D` e `AbnerCruz/Urbe` são **espelhos de distribuição** (releases e Urbe Web; arranjo transitório, DEC-0008/DEC-0009): o código só muda aqui. Detalhes: [`docs/migration/README.md`](docs/migration/README.md).

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
