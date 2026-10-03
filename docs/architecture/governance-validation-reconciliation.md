# Reconciliação de validações e identidade de tarefas — P4-11

Evidências observadas em 2026-10-03, base `d629af882cd9580adc6e2fc0174c96a1d7754db0`.
Este documento registra investigação/migração; estado vivo continua nas Issues,
escopo/IDs no ROADMAP, respostas humanas nos registros canônicos existentes.

## Causas verificadas

- Issue #114, run 37128804541, job 111219548540: aplicador retornou `registered`,
  mas consistency recusou VR Hub dev.4: evidência humana `pending` duplicada
  divergia do handoff recém-atualizado `passed`. Verificar retornou código não zero;
  gravação/Pages foram corretamente omitidos e veredito falhou. O passo Aplicar
  aparece success porque captura o código como output; isso não comprova registro.
  A duplicação do VR já foi removida no PR #115; #117 depois registrou o mesmo
  resultado real. Não substituir o registro #117 nem exigir novo clique #114.
- Issue #125, run 37133027012, job 111231776480: aplicador retornou `rejected`
  porque o handoff releases-diretas, de outro trabalho, era o mais recente para
  o mesmo ID P4-8. Verificar/gravar/Pages foram omitidos; veredito fail closed
  funcionou corretamente. A recusa decorreu da colisão de identidade.
- O contrato `registered`/`already-registered` já era correto para ambas as
  operações. Nenhum commit em repetição idempotente é sucesso, não erro.
  `failed` como escolha humana válida também é sucesso do mecanismo.

## Correção e segurança

A aplicação atualiza transacionalmente resposta, handoff e evidências por build
que já referenciem exatamente aquele handoff/check humano. Não cria escopo de
validação, não muda a tarefa para done e não promove automaticamente o build
para VALIDATED. Reprovação remove estados de build incompatíveis; erros de escrita
restauram os arquivos. O workflow inclui docs/validation na gravação/rollback e
republica Pages também para repetição idempotente válida. Actor, hash, objeto,
resultado incompatível e substituição legítima de handoff continuam fail closed.

O checker rejeita ID de tarefa duplicado em qualquer fase; self-tests exercitam
P0 e P4. Os testes do aplicador/veredito cobrem aprovação, repetição sem commit,
conflito, reprovação/repetição, hash/objeto/actor/handoff inválidos e VR vinculado.
Fixtures das Issues reais #114/#125 são executadas em cópia temporária isolada;
não criam respostas humanas nem sobrescrevem os arquivos canônicos da main.

## Migração mínima de identidade

Primeira integração canônica de P4-8: Patch Notes, PR #122, main `5813856`,
2026-10-03T15:10Z (head fd18656). Releases diretas chegou depois pelo PR #123,
main `2605d22`, 2026-10-03T15:22:25Z (head 4833c24). Logo Patch Notes preserva
P4-8; releases diretas recebe P4-10 (livre na base), mantendo Issue #120,
message_id, PR e evidências. P4-9 exige releases diretas, portanto depende de
P4-10. Nenhuma renumeração das outras tarefas.

A resposta #126 `VAL-HO-20261003-releases-diretas-1093808f3a06.md` conserva
seu título/corpo P4-8 exatamente como enviados/registrados naquele momento.
A associação inequívoca permanece pelo message_id e objeto. Não fabricar
uma resposta com título P4-10 atribuída ao proprietário.

## Auditoria da Fase 4 e recuperação após integração

| Item | Evidência atual | Próxima ação legítima |
|---|---|---|
| P4-1 | PR #102 integrado, #105 aprovada, dev.3 VALIDATED | Concluído; preservar [x] |
| P4-2 | PR #108 integrado, dev.4 publicado, #117 registrou aprovação equivalente à #114 | Reprocessar #114 idempotentemente; fechamento documental com evidência existente |
| P4-3 | Nenhum instalador integrado | Continuar [ ]; não inferir aprovação do gate |
| P4-4 | Instalação/atualização standalone ainda não validada | Continuar [ ]; DEVICE obrigatório |
| P4-5 | PR #104 integrado, dispatch confirmado por releases seguintes | Concluído; preservar [x] |
| P4-6 | #112 decidiu A, ADR-0018 Aceito no PR #115 | Reconciliar conclusão documental; não implementar instalação nesta correção |
| P4-7 | PR #115 integrado, #118 aprovou aparelho; handoff CI ainda pending | Conferir CI/publicação e então reconciliar evidência/fechamento |
| P4-8 | PRs #122/#124 integrados, dev.6 e CI/hash conferidos, #125 aprovada mas recusada | Após corrigir ID, reprocessar evento #125 existente; registrar e fechar com DoD |
| P4-9 | Sem corte Urbe, chave/ponte ainda pré-requisitos | Continuar [ ], dependência P4-10 |
| P4-10 | PR #123 integrado, #126 aprovada; handoff ainda possui checks automatizados pending | Conferir CI/primeira release antes de fechamento; não supor todos verdes |
| P4-11 | Correção de mecanismo e identidade deste PR | Crítico por control plane; integração pelo integrador após autorização |

Reexecutar as execuções originais #114/#125 somente após a correção estar na main:
checkout do workflow usa a branch padrão atual, preservando os eventos reais.
Não alterar Issues de validação, criar aprovação nova ou pedir novo clique.
A reconciliação dos estados de tarefas fica em PR documental posterior, com
resultados reais e critério de integração/CI/DEVICE já satisfeitos. Enquanto a
correção crítica não integrar, as pendências reais permanecem explícitas.
