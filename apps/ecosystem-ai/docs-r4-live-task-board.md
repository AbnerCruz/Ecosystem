# P6-5 R4 — Quadro visual de tarefas e agentes auditados

Incremento do painel local **C# / ASP.NET Core** já entregue na PR #402.
Mantém o mesmo `LocalProjectStore` de projetos/sessões/receipts, o
`LocalRunEventLog` de eventos e o **`RunState.Replay` do AgentRuntime.Core**.
Nenhum banco de dados, runner, agenda ou ledger alternativo é criado.

## Uso

Com o catálogo e o journal previamente gerados por execuções reais do Product:

```bash
dotnet run --project src/EcosystemAi.Cli -- \
  --web-ui --catalog /dados/privados/catalogo \
  --journal /dados/privados/runs --port 8765
```

Abra `http://127.0.0.1:8765/` **no mesmo dispositivo**. O painel continua
limitado a loopback e sem JavaScript. Sem `--journal`, o comportamento visual
da PR #402 permanece inalterado: projetos, sessões, recibos e conversas, sem
afirmar que o journal foi consultado.

Com `--journal`, cada consulta GET refaz a leitura e o replay do Runtime:
- quadro expansível de **agentes com execuções verificáveis**, com links às
  tarefas associadas; contagem de agentes, runs, verificações aprovadas e
  recibos sem evidência no journal;
- dados do **run**: referência de tarefa (`ReasonRef`, não seu prompt), id
  de agente, modelo, estado reconstituído, verificação, passos, tool calls;
- índice de artefatos referenciados por esse run, somente nome e tipo, sem
  caminhos ou conteúdo. Um artefato não é um arquivo incorporado ou editável;
- divergência entre estado do recibo e replay exibida como alerta; recibo
  sem journal indica **ausência de evidência**, nunca sucesso presumido.

O journal continua sendo a autoridade do estado de execução. A UI é
**somente uma projeção efêmera**, sem novos registros persistentes. IDs,
agentes, tarefas, modelos e nomes de artefatos são sempre HTML-encoded.
Sem prompt, resposta do modelo, argumentos de ferramenta, payload/result,
dados privados de verificação ou leitura de arquivos do workspace nesta rota.

## Segurança e limites

- Reaproveita o Host HTTP local da PR #402: loopback, Host/Origin/CSRF nos
  formulários, CSP, bloqueio de recursos remotos, sem cache e sem JS.
- Pasta do journal deve existir, estar fora de todos os workspaces, e não
  passar por ancestral simbólico. A consulta recusa logs inválidos e estados
  irrecuperáveis; o painel retorna erro em vez de exibir informação inventada.
- Máximo **250 runs distintos por consulta auditada**; sem background polling
  nem observação contínua. Recarregue a página para refletir novos registros.
- Esta UI **não chama um modelo, não concede permissão, não pode iniciar
  uma tarefa ou gastar créditos**. A execução real permanece no modo CLI
  `--chat` / `--goal`.
- Tarefas exibidas são **referências de execuções já registradas** no
  Runtime, não gestão de tarefas futuras, plano de equipes ou uma fila.
  Agentes mostrados são os agentes dos eventos reais, não perfis inventados.

Ainda faltam gestão de perfis/equipes e submissão de tarefas pela interface
(uma alteração de fronteira de confiança que requer autorização específica),
além de uma UX Android distribuível. P6-5 e Issue #354 permanecem abertas.

## Evidência

`R4WebAuditTests`: UI via HTTP loopback com `LocalProjectStore` e
`LocalRunEventLog` reais, journal válido, referências escapadas, payloads e
caminhos privados omitidos, revisão imutável, receipt sem journal, log
corrompido fail-closed, journal no workspace recusado, regressão da UI padrão.
Executar suite `AgentRuntime.Tests` e `consistency` do repositório.
