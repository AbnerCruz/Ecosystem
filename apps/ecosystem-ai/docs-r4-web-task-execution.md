# P6-5 R4 — Envio supervisionado de tarefas pelo painel C#

Uma etapa operacional do Product Ecosystem AI: o painel `--web-ui`
agora pode **submeter tarefas reais ao AgentWorkspace.WorkspaceSession**
sem outra implementação do Runtime/ledger/provider. A rota `POST /tasks`
é criada **somente** quando o operador liga `--web-tasks`.
Todos os demais comandos e a interface anterior continuam sem invocar modelos.

## Usar

Em `apps/ecosystem-ai`, com .NET 10 e um catálogo com projetos/sessões
registrados, escolha o provedor e o custo **ao iniciar o servidor**:

```bash
# export ECOAI_API_KEY=... (a chave nunca vai ao argumento ou formulário)
dotnet run --project src/EcosystemAi.Cli -- \
  --web-ui --web-tasks --catalog /dados/privados/catalogo \
  --journal /dados/privados/runs \
  --endpoint https://PROVEDOR/chat/completions --model MODELO \
  --budget-cents 20 --max-call-cents 5 \
  --web-process-budget-cents 100 --web-max-runs 8 \
  --input-usd-per-million 1 --output-usd-per-million 3
```

Abra `http://127.0.0.1:8765/` **no mesmo dispositivo**. Cada sessão
registrada apresenta um campo de tarefa e botão **Executar tarefa**.
O formulário envia exclusivamente `csrf`, `projectId`, `sessionId` e
`goal`; o usuário não consegue trocar modelo, endpoint, orçamento,
capabilities ou pasta por valores de formulário. A própria URL de POST
nem existe quando `--web-tasks` não está presente.

O comando **não admite `--allow-create`**: somente leitura de arquivos
(`files.read`), como no modo seguro da CLI. `--web-with-history`
também é opt-in do operador, e autoriza reenviar ao provedor o histórico
limitado da sessão. Sem essa flag, só a nova tarefa vira contexto.

## Custos e operação

- Orçamento por run em centavos USD: `--budget-cents` e
  `--max-call-cents`, com fiscalização do **Ledger original**.
- `--web-process-budget-cents` é um teto local adicional para uma
  instância do servidor, que reserva o orçamento **máximo de cada run
  antes** de invocar o provedor. Uma reserva não é restituída após falha,
  resposta barata ou cancelamento. O último run recebe o saldo restante.
- `--web-max-runs` limita os envios dessa instância (padrão 8, no
  máximo 100). Com a quota esgotada, botões ficam inativos e POSTs
  retornam 429. Um segundo POST durante uma tarefa ativa recebe 409,
  sem fila nem retries em background.
- A UI continua lendo o catálogo canônico; o executor continua gravando
  mensagens, receipts e eventos no mecanismo existente.
- **Limites:** quota por processo não é limite distribuído entre
  dispositivos/instâncias, nem garantia de fatura final. O processo pode
  ser reiniciado com novas quotas; estimativas de preço são do operador.

## Fronteira de confiança

Ativar `--web-tasks` permite ao navegador local iniciar **ações que
podem custar dinheiro**. Isto é uma ampliação real de capacidade e exige
revisão da classificação crítica e autorização do proprietário antes da
integração, de acordo com a governança do Ecosystem.

A superfície continua 127.0.0.1 com validação rígida de Host contra DNS
rebinding, Origin e token CSRF de 256 bits, limite de Content-Length, CSP
sem JavaScript/iframes e sem cache. Não expõe chave de API no HTML nem
retorna erros internos do provider ao navegador. A chave só vem de
`ECOAI_API_KEY` no processo Host. O CLI faz a requisição HTTP ao provedor
com redirects desabilitados. Nenhuma operação destrutiva é permitida.

**Atenção:** a chamada real pode demorar, pois o servidor aguarda um
run completo (não há streaming ou cancelamento fino via UI nesta etapa).
O operador pode encerrar o processo local. Isto ainda **não é** APK.

## Evidência

`R4WebTaskExecutionTests` testa POST loopback real, uso de um
mesmo executor, histórico opt-in, orçamento reduzido no segundo run,
quota esgotada, ausência da rota quando não habilitada, Host/Origin/CSRF,
campos extras, IDs inválidos, concorrência, falha sem retry, isolamento
de workspace e nenhum grant de escrita. A suíte existente do Product
também cobre o provider mockado e seus limites de Runtime.

**P6-5 continua aberta:** ainda faltam perfis e equipes configuráveis,
gerenciamento de tarefas além do envio manual, limites distribuídos e
distribuição/validação Android.
