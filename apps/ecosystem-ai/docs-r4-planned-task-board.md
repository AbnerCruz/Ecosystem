# P6-5 — Quadro de tarefas do Product (R4)

O Ecosystem AI agora tem um quadro de tarefas **planejadas** no painel
C# local. Cada tarefa pertence a um projeto e sessão já existentes e
possui título, objetivo, critério de aceite declarado e executor
(assistente, agente ou equipe do roster). **Planejar não chama modelo**,
não gasta tokens e não cria ações em background.

A tarefa só executa quando o operador clica **Executar agora**. O Host
precisa ter sido iniciado explicitamente com `--web-ui --web-tasks`,
modelo, endpoint e orçamento configurados. O POST usa apenas `taskId`
e token CSRF. O servidor resolve toda a definição pelo catálogo privado,
não recebe grant, custo, modelo nem texto alterado no envio. Usa
`CliWebTaskRunner.SubmitAsync` e, portanto, o MESMO
`WorkspaceSession → AgentRunner/Ledger` existente, inclusive revisão de
equipe com `--web-review-teams` quando autorizada.

## Persistência e evidências

- O auxiliar `planned-tasks.json` está ao lado de `catalog.json`,
  com versão 1, revisão, checksum SHA-256, lock e rename atômico.
  **Não muda nem migra o catálogo v1**, que continua sendo a fonte de
  verdade de turnos, runs, custos e verificação.
- Cada tentativa registra somente outcome, horário e **IDs dos
  RunReceipts reais** que já constam da sessão, não duplica respostas.
- `response_verified` exige exatamente um recibo `succeeded` e
  `Verified=true`. `review_recorded` exige dois recibos verificados
  e parecer de equipe já registrado. `unverified` sinaliza ausência
  de evidência; `failed` sinaliza uma falha da execução. Nenhum desses
  status equivale a `TaskRecord.Completed` ou aprovação dos critérios.
- O usuário pode **cancelar** um planejamento, impedindo novas
  execuções pelo quadro. Runs já concluídos não são apagados.
- Até 500 tarefas e 20 tentativas por tarefa; arquivo até 3 MiB.
  Criação não permite IDs de agente/equipe de outro projeto.
- Há apenas um run web ativo por servidor; cada nova tentativa é um
  gesto humano deliberado. Sem worker, polling, cron ou retry automático.
- A comparação de recibos é conservadora (prefixo estável do histórico
  e submissão textual rastreável). Outra CLI independente escrevendo
  simultaneamente pode impedir a associação segura; nesse caso não
  declarar sucesso. Para auditoria financeira distribuída ainda falta
  um identificador de correlação propagado pelo Runtime.

## Segurança

Painel somente em 127.0.0.1, com Host/Origin/CSRF, formulário estrito,
sem JS e HTML escaped. No modo sem `--web-tasks`, planejamento funciona
sem rota de execução. O Runtime é read-only (sem `--allow-create` pelo
navegador), mantém orçamento por run e orçamento por processo.

Testes `R4PlannedTaskBoardTests`: HTTP real, criação sem custo, seleção
de executor, execução mockada pelo Runtime existente, associação do
recibo, cancelamento, quota, XSS/HTML escaping, isolamento de projetos,
corrupção de checksum, CSRF, campos extras, ausência de `/task-run`
sem opt-in.

**Ainda pendente no roadmap P6-5:** validação de critério de aceite
material (não apenas resposta presente), integração real de ChangeSets
pelo `ChangeIntegrator`, experiência Android distribuível e ensaio
manual com provider real. A Issue #354 permanece aberta.
