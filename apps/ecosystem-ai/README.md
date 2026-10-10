# Ecosystem AI

AI Product do ecossistema: **consumidor** do Agent Runtime. Nasce com o runtime mínimo (slice **R1**) dentro dele, com **fronteira de extração** explícita; qualquer promoção para componente compartilhado passa por Extraction Review (NN-022).

Autoridades: [ADR-0017](../../docs/adr/0017-agent-runtime-execution-runtime-e-organizacoes.md) (arquitetura), [`docs/architecture/agent-runtime.md`](../../docs/architecture/agent-runtime.md) (plano e testes de enforcement), `ecosystem.json` (identidade, versão e permissões) e o ROADMAP (escopo, item P6-2). Este README não replica fase nem status.

## O que existe (R1)

| Projeto | Responsabilidade |
|---|---|
| `src/AgentRuntime.Core` | Primitivas e o laço do agente: Context, orçamento (ledger), ferramentas como capabilities, log de eventos, estado de tarefa com verificação, retomada. **Sem dependência alguma** além da biblioteca-base. |
| `src/AgentRuntime.Testing` | Implementações determinísticas das portas, para teste: provedores Scripted e Recorded, relógio, log (com injeção de falha), aprovadores. |
| `src/AgentRuntime.Tools.Files` | Tool de exemplo: `files.read`, `files.write` e `files.delete` presas à raiz do Context, mais o verificador de arquivos. |
| `tests/AgentRuntime.Tests` | Suíte de enforcement (T-1…T-15 do plano), incluindo testes de arquitetura lidos dos metadados do IL. |

## Garantias (cada uma tem teste)

- **Deny-by-default.** Ferramentas disponíveis = Host ∩ organização ∩ projeto ∩ agente; sem `agent.act` nenhuma. Fora do escopo de Context, a ferramenta não existe para o run.
- **Orçamento é primeira classe.** Toda chamada reserva antes e concilia depois; sem limite configurado, não gasta; estouro bloqueia, nunca é absorvido.
- **Saída ≠ sucesso.** Uma tarefa só chega a `Completed` com verificação independente aprovada e com evidência; critério que o verificador não entende falha.
- **Operação destrutiva exige aprovação.** Sem aprovador, o runtime escala em vez de agir.
- **Observável e auditável.** Todo evento responde quem, por quê, ferramenta, modelo, custo, contexto, resultado, verificação e aprovação. Não existe campo de raciocínio livre; o raciocínio privado do modelo é descartado.
- **Segredos nunca vazam.** Valores registrados são redigidos de eventos, artefatos e do que o modelo vê.
- **Retomável.** O estado do run é reconstruído do log; um log adulterado é recusado.
- **Laços têm limite.** Falhas equivalentes, repetição sem progresso, tentativas de verificação e passos têm teto.
- **Ocioso custa zero.** O host é dirigido por eventos: sem timer, sem espera ocupada, sem chamada a provedor.

## Fronteira de extração

O Core não referencia pacote nem outro projeto, e os testes de arquitetura (T-13) falham se ele passar a conhecer rede, processo, arquivo, timer, relógio direto, UI, SDK de provedor, outro Product ou o Control Plane do ecossistema. É essa fronteira que permite uma futura extração sem reescrita. Enquanto houver um único consumidor real, o código fica aqui.

## Como testar

```bash
dotnet test --project apps/ecosystem-ai/tests/AgentRuntime.Tests
```

Requer .NET SDK 10+. Não há provedor de modelo real, rede nem segredo: o R1 prova o runtime com provedores determinísticos.

## Coordenação local candidata

A prova de equipes, isolamento e integração está descrita em [docs-r2.md](docs-r2.md).
Autoridade do escopo: P6-3 no ROADMAP; contrato candidato: ADR-0020.

## Sessão do Agent Workspace

A prova local de hospedagem em dois contextos está em [docs-r3.md](docs-r3.md).
O Workspace consome o Core. A prova técnica P6-4 foi concluída com o **Host real Lunet Core** por composição headless em `tests/integration/p6-lunet-host` (PR #344, ADR-0031, DEC-0041), sem UX de agentes no APK ou dependência entre Products. A interface do Ecosystem AI é escopo P6-5; o Agentic Workspace visual do Lunet continua na Fase 8.

## Superfície visual offline (R4, incremento de leitura)

O Product já pode gerar uma visualização HTML **estática, mobile-first e somente leitura**
a partir do catálogo local, sem servidor nem modelo. Os projetos, sessões,
mensagens e recibos são lidos do `EcosystemAi.ProjectStore`, não do Runtime.
Essa projeção não encerra P6-5 nem substitui a futura UI interativa.

Uso, segurança e limitações: [docs-r4-visual-snapshot.md](docs-r4-visual-snapshot.md).

## Gestão local de projetos e sessões (R4)

A CLI também permite vincular um workspace existente como projeto, criar sessões
independentes e navegar nos registros do catálogo pelo terminal sem executar
agentes, acessar rede ou exigir token. Os registros são do mesmo
`LocalProjectStore` usado por runs, histórico e visualização offline.

Comandos `--create-project`, `--create-session` e `--manage`, limites e exemplos:
[docs-r4-catalog-management.md](docs-r4-catalog-management.md).
É um passo funcional intermediário, **não** a UI Android definitiva.

## Chat interativo no terminal (R4)

O modo `--chat` permite enviar várias mensagens em uma sessão existente, usando
**o mesmo `AgentWorkspace.WorkspaceSession`** e as políticas de cada execução.
Cada mensagem gera seu próprio run/receipt; o histórico anterior só é reenviado
ao provider no modo chat solicitado, com limites explícitos. A interface C# de
terminal não substitui a futura UI gráfica mobile-first.

Exemplo de comandos, isolamento, custos por mensagem e restrições:
[docs-r4-interactive-chat.md](docs-r4-interactive-chat.md).

## Painel gráfico local (R4)

A interface responsiva de projetos/sessões agora pode ser servida **somente em loopback**
pelo próprio Product C# (sem modelo ou JavaScript):

```bash
dotnet run --project src/EcosystemAi.Cli -- --web-ui --catalog /dados/privados/catalogo --port 8765
```

No mesmo dispositivo, abra `http://127.0.0.1:8765/` no navegador. A tela
permite vincular pastas preexistentes como projetos, criar sessões, ler
conversas, execuções e custos; grava diretamente no `LocalProjectStore` atual.
Isso **não é APK nem acesso remoto** e ainda não habilita execução de agentes
no navegador. Documentação e riscos: [docs-r4-local-web-ui.md](docs-r4-local-web-ui.md).

## Quadro de atividade real de agentes e tarefas (R4)

Com `--web-ui --catalog /dados/privados/catalogo --journal /dados/privados/runs`
o painel local exibe agentes, tarefas, verificações, ferramentas e artefatos
**de execuções reais**, reconstruídos diretamente do `RunState.Replay`.
Não lê prompts/payloads nem cria motor, ledger ou armazenamento paralelo;
ainda não permite disparar agentes pelo navegador. Ausências ou divergências de
journal são sinalizadas, e a UI padrão sem `--journal` não é alterada.

Uso, limites e testes: [docs-r4-live-task-board.md](docs-r4-live-task-board.md).

## Prévias textuais opcionais no painel local (R4)

A flag `--web-ui --catalog DIR --journal DIR --embed-text-artifacts` habilita
prévia **do conteúdo atual** de arquivos textuais referenciados nos runs do
Runtime. O leitor reutiliza os limites já testados de 16 KiB por arquivo,
128 KiB por consulta e 16 arquivos, sem acesso fora do workspace ou por
symlinks, e HTML escapado. Desativada por padrão; pode revelar dados privados.

Limites e uso: [docs-r4-live-artifact-previews.md](docs-r4-live-artifact-previews.md).

## Teto de orçamento da instância de chat (R4)

`--chat ... --session-budget-cents N` contabiliza os recibos anteriores em USD
e **reserva o limite inteiro de cada próximo run** antes de enviá-lo ao
Workspace. Reduz o orçamento por turno quando o teto está próximo; a sobra
não é devolvida. Não é teto distribuído entre processos e não substitui a
fiscalização do Ledger nem garante valores faturados pelo provedor.

[Limites e exemplos](docs-r4-chat-session-budget.md).

## Enviar tarefas reais diretamente pelo painel local (P6-5)

O painel web C# agora pode receber tarefas **explicitamente** através de
`--web-tasks` junto com endpoint, modelo, preço e limites fixados pelo
operador na inicialização. Não recebe credenciais nem concede `fs.write`
no navegador; o formulário só escolhe projeto, sessão e tarefa.

Há limite de execuções e orçamento reservado por instância do servidor,
sem fila nem retry automático. Isto amplia a fronteira de confiança por
permitir gastos através de HTTP local e **depende de autorização crítica
para integrar**. [Uso, segurança e limitações](docs-r4-web-task-execution.md).

## Agentes e equipes no painel (R4)

O painel local agora cadastra **agentes e equipes por projeto**, com produtor e
revisor distintos. A execução supervisionada permite escolher um agente ou o
produtor da equipe, usando o **mesmo Runner, modelo, orçamento e grants read-only**.
A revisão de equipe não é automática nesta etapa. Os perfis são gravados em
`roster.json` ao lado do catálogo, sem alterar o schema de `catalog.json` v1.
[Limites e testes](docs-r4-project-agents-teams.md).

## Revisão independente opcional para equipes (R4)

`--web-ui --web-tasks --web-review-teams` executa primeiro o **produtor** e,
se houver recibo verificado e resultado real, executa um **segundo run com o
revisor independente**. A quota da instância deve suportar ambos; cada run
usa o mesmo Runtime, modelo e grants de somente leitura. O parecer não
aprova nem integra arquivos automaticamente. Sem a flag, as equipes continuam
no comportamento anterior (somente produtor).

Detalhes e evidências: [docs-r4-independent-team-review.md](docs-r4-independent-team-review.md).
