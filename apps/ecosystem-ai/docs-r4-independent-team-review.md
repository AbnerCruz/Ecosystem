# P6-5 R4 — Dupla execução com revisão independente de equipe

O Ecosystem AI agora dispõe de um fluxo opcional de equipe com **dois
agentes de verdade**: produtor e revisor independente. A etapa é aditiva
sobre a interface web C# já autorizada na PR #406 e sobre os perfis/equipes
da PR #408. Continua usando o mesmo **EcosystemAiCli.RunAsync →
WorkspaceSession → AgentRunner/Ledger**. Não há segundo runner.

## Uso

Configure o Product como antes, acrescentando `--web-review-teams` **com**
`--web-ui --web-tasks`. A equipe é selecionada na sessão por seu nome,
e o navegador pode solicitar a execução. A flag fica desativada por
padrão: sem ela, escolher equipe continua executando **somente o produtor**.

```bash
dotnet run --project src/EcosystemAi.Cli -- \
  --web-ui --web-tasks --web-review-teams \
  --catalog /dados/privados/catalogo --journal /dados/privados/journal \
  --endpoint https://PROVEDOR/chat/completions --model MODELO \
  --budget-cents 20 --max-call-cents 5 \
  --web-process-budget-cents 100 --web-max-runs 6 \
  --input-usd-per-million 1 --output-usd-per-million 3
```

O navegador continua em **http://127.0.0.1:8765** apenas no dispositivo
que executa .NET. Tokens e endpoint não vão ao formulário.

## Protocolo de execução

1. Validar projeto, sessão, IDs do produtor e do revisor no roster local.
   Instanciar o `TeamPlan` do **AgentRuntime.Core**, exigindo funções
   distintas, tarefa e critério de aceite.
2. Verificar **antes** de gastar se há pelo menos duas execuções e dois
   orçamentos máximos disponíveis nessa instância do Host. Não iniciar
   produção que não tenha quota suficiente para revisão.
3. Executar o **produtor** como run próprio read-only, com identidade
   carregada do perfil. O retorno 0 do CLI, sozinho, **não basta**:
   exigir exatamente um novo `RunReceipt` succeeded/verified e resposta
   `assistant` no catálogo canônico.
4. Construir a tarefa do revisor com solicitação original e trecho limitado
   da resposta **já redigida e persistida** do produtor. Ela é tratada
   como dado não confiável, não como instrução. Encaminhar esse trecho
   ao mesmo provider é uma consequência explícita de habilitar a flag.
5. Executar **revisor diferente do produtor**, com seu próprio perfil,
   runId, receipt, orçamento e resposta. Validar também esse segundo
   recibo e a resposta. A tela do catálogo mostra os dois runs e parecer.
6. Não chamar `ChangeIntegrator` nem `TeamPlan.Record`. Não fabricar
   `IntegrationReceipt`. Um parecer é **revisão realizada**, mas não
   equivale a aprovação confiável nem integração de um ChangeSet.

Um erro, ausência de recibo ou falta de verificação interrompe a sequência
e informa **revisão incompleta**, sem fingir sucesso. Não há retries
automáticos, execução em background nem escrita no workspace.

## Custos e limites

- Cada equipe completa utiliza **dois runs potencialmente pagos**.
- Exige previamente orçamento de **2 × --budget-cents** e duas posições
  disponíveis em `--web-max-runs`, independentemente dos custos efetivos.
- A reserva do produtor ocorre antes do run e a do revisor antes do segundo.
  Não há devolução de reservas; modelo e preço são definidos pelo Host.
- O orçamento é local à instância; **não** é teto distribuído entre
  processos nem estimativa infalível da fatura do provider.
- A passagem de trecho do resultado do produtor ao revisor é limitada a
  7.000 caracteres; a tarefa original, a 3.500 caracteres.
- O verificator de presença de resposta do Runtime **não prova a
  qualidade** nem que o parecer seja correto. Auditoria estrutural e
  aceite técnico integral permanecem exigências diferentes.

Os testes `R4IndependentTeamReviewTests` cobrem sequência/identidades,
custo reservado, ausência de receipt, falta de quota, flags inválidas,
regressão de equipe de um run e superfície HTML em localhost.

**P6-5 ainda aberta:** pareceres não viram aprovação nem integram mudanças.
O fechamento exigirá revisão material/evidência de um `ChangeSet` pelo
`ChangeIntegrator` e validação Android do Product distribuível.
