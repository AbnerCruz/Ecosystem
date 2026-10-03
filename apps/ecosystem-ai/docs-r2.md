# R2: prova local de produção, revisão e integração

Especificação candidata: [ADR-0020](../../docs/adr/0020-r2-workspace-changes-e-integracao-local.md).
Escopo e andamento: ROADMAP P6-3 e Issue #136.

O produtor recebe capabilities presas a um `IsolatedWorkspace`; seu resultado é
um `ChangeSet` selado. O Host passa a proposta ao `ChangeIntegrator`, que consulta
revisor independente, verificador do combinado e política confiável. Só uma
receipt `Integrated` libera dependências no `TeamPlan`. Receipts contêm tarefa,
produtor, candidato, revisão, evidência e revisão canônica; o Host registra isso.

Os objetos são imutáveis onde cruzam a fronteira de revisão; o overlay permanece
mutável e privado ao produtor. Paths são relativos, sem traversal, barras inversas,
unidades ou caracteres de controle. Isso não é uma sandbox de disco: o adapter de
filesystem futuro deve ainda validar symlinks, nomes e colisões por plataforma.

Dentro de `apps/ecosystem-ai`, execute `dotnet test --project tests/AgentRuntime.Tests`
(o `global.json` local seleciona Microsoft.Testing.Platform).
`TeamIntegrationTests` prova dois agentes com providers roteirizados: um produz
por capability de escrita limitada; outro lê o combinado e verifica o resultado.
Outros cenários recusam autorrevisão, evidência antiga, conflitos, ciclos,
autorização de ator errado e cancelamento antes da publicação. Não requer teste
em aparelho: este slice não contém UI ou lifecycle Android.

O cancelamento é conferido entre cada porta confiável: revisão, verificação,
classificação e autorização. Uma porta que cancela a execução impede chamadas
às portas seguintes, inclusive pedidos de autorização. Exceções preservam o
snapshot canônico, não consomem o ID da mudança e liberam o integrador para retry.
Os testes exercitam cancelamento e exceções nas quatro etapas.

Não há protocolo público/JSON, armazenamento durável ou adapter Git neste slice.
A política deve vir do Host; jamais classificar por uma declaração de risco do
produtor. O integrador real do repositório continua independente.
