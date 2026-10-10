# P6-5 R4 — Teto conservador para uma instância de chat

A sessão de terminal `--chat` pode receber `--session-budget-cents N`,
um limite extra de contenção de custo em **centavos USD**. A opção é
**opt-in** e preserva o comportamento anterior do chat quando ausente.

Exemplo de uso (demais opções de modelo, endpoint, preços e IDs obrigatórias):

```bash
dotnet run --project src/EcosystemAi.Cli -- \
  --chat --catalog /dados/privados/catalogo \
  --project /pasta/existente --project-id ID --session-id ID \
  --endpoint https://PROVEDOR/chat/completions --model MODELO \
  --budget-cents 30 --max-call-cents 5 \
  --session-budget-cents 75 \
  --input-usd-per-million 1 --output-usd-per-million 3
```

A chave continua vindo exclusivamente de `ECOAI_API_KEY`, nunca do
argumento CLI. O chat continua usando **EcosystemAiCli.RunAsync →**
**AgentWorkspace.WorkspaceSession → AgentRunner → Ledger existentes**.
Nenhum novo ledger, tabela de saldo, protocolo nem armazenamento é criado.

**Semântica:** ao abrir o chat, contabiliza os `RunReceipt` já existentes
na sessão escolhida, em USD, incluindo custos estimados. Se houver recibos
em outras moedas, recusa misturar as unidades. O saldo local é o teto menos
esse total já registrado. Para cada mensagem, **antes** de chamar o
executor, reserva `min(orçamento por run, saldo local)` por inteiro,
ajusta `--budget-cents` e `--max-call-cents` se necessário e diminui
o saldo. O limite ainda é fiscalizado pelo Ledger do Runtime em cada run.

A sobra de uma reserva **não é devolvida**, mesmo se a resposta custar menos
do que o máximo permitido. Isso é deliberado e conservador: uma execução
interrompida sem receipt não reabre o orçamento na mesma instância.
Quando a reserva atinge zero, o chat recusa novas chamadas ao provider.

**Limites importantes:** este é um limite da **instância atual do chat**,
não uma garantia de faturamento acumulado da organização. Outros processos
em paralelo não compartilham essa reserva; ao reiniciar, gastos sem receipt
podem não ser contados. Provedores podem cobrar valores diferentes das
estimativas fornecidas pelo operador. O teto por run e a fiscalização do
Runtime continuam obrigatórios, não são substituídos por esta opção.
Para um teto global forte ainda serão necessárias políticas de custo/
orçamento da organização e decisões de confiança do Host.

O usuário continua podendo encerrar em `/sair`, e `--allow-create` é uma
permissão explícita separada. Esta entrega não amplia capabilities, não
inicia tarefas por HTTP, não expõe credenciais e não fecha P6-5.

`R4ChatSessionBudgetTests` prova reservas de dois turnos, ajuste do último
budget/max-call, conta separada de outros chats, recibos prévios, recusa de
outra moeda, teto zerado e flags inválidas. Os testes de contrato do Runtime
e provider HTTP continuam os existentes.
