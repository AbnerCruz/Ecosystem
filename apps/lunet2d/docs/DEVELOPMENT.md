# Rotina de desenvolvimento

Esta rotina vale para todo trabalho no Lunet2D, feito por pessoa ou por agente. O objetivo é nunca perder a direção: o planejamento é o [`docs/SPEC.md`](SPEC.md), o estado é o [`ROADMAP.md`](../ROADMAP.md).

## 1. Antes de começar qualquer trabalho (sempre)

0. Ler `AGENTS.md` e `AgentsChat.md`; conferir alterações locais, branches e PRs abertos antes de assumir trabalho.
1. Ler `ROADMAP.md`, seção "Estado atual" e a fase em andamento.
2. Rodar `bash tools/roadmap-status.sh --pending` para ver o que já foi feito e o que falta, por fase.
3. Reler no `docs/SPEC.md` as seções citadas nos itens escolhidos (a fase inteira, quando a fase mudar).
4. Escolher o próximo trabalho pela **ordem das fases**: primeiro as pendências da fase atual, depois a próxima fase. Itens da fase seguinte só entram se estiverem listados em "Transversal" ou se o usuário pedir explicitamente.
5. Dizer em uma frase qual item do ROADMAP será feito. Se o trabalho não corresponde a nenhum item, **primeiro** acrescentar o item ao ROADMAP (com a seção do spec) e só então implementar.

## 2. Ciclo de cada item (§36)

```
implementar → build → testar → rodar/inspecionar → corrigir → documentar
→ atualizar ROADMAP → atualizar CHANGELOG → commit → push → PR → CI verde → merge
```

- Commits pequenos e coerentes, no formato `tipo(escopo): descrição`.
- Testes do que muda; o que depende de Android é validado no aparelho (release de desenvolvimento) e o ROADMAP diz qual dos dois estados vale: "feito, sem validação em aparelho" ou `[x]`.
- Marcar `[x]` só com implementação + testes + documentação + integração (§24). Na dúvida, deixar `[ ]` e escrever o que falta.
- Limitação real nunca vira falsa implementação: documente o bloqueio no ROADMAP, preserve o build e siga com itens independentes (§31).
- Decisão arquitetural nova ou mudança de hipótese → ADR em `docs/adr/`.

## 3. Entrega

- PR com CI verde é **mergeado** (decisão do responsável do projeto). A `main` gera a release de desenvolvimento com APK, checksums e notas.
- Depois do merge, confirmar que a release saiu e passar ao usuário o link e um roteiro curto de teste no aparelho para o que é novo.
- Falha de CI: corrigir a causa, não contornar; nunca desabilitar ou pular teste.

## 4. Checagem completa ao concluir uma fase

Uma fase só é considerada concluída quando **todos** os itens dela estão `[x]` (ou foram explicitamente movidos/adiados com motivo e destino no ROADMAP) **e** existe um registro de auditoria em `docs/audits/fase-N.md` (modelo em [`docs/audits/TEMPLATE.md`](audits/TEMPLATE.md)). A auditoria cobre:

1. **Gate da fase** (§28): o critério de aceite descrito no spec, verificado no aparelho quando aplicável, com a versão testada.
2. **Itens da fase**: cada caixa do ROADMAP revisada contra o código real (existe, está testada, está documentada, está integrada ao app).
3. **Spec**: reler as seções do spec ligadas à fase e listar qualquer requisito não coberto; cada lacuna vira item no ROADMAP.
4. **Princípios (§2)**: offline first, sem conta/telemetria obrigatórias, projetos como arquivos normais, APIs públicas sem vazamento de detalhes, ferramentas oficiais sobre as mesmas APIs dos plugins.
5. **Qualidade (§30)**: sem TODO crítico escondido, botão sem comportamento, exceção ignorada, API pública sem documentação, código morto, duplicação de infraestrutura.
6. **Arquitetura**: regras de dependência (teste `ArchitectureTests`), ADRs em dia.
7. **Regressão**: suíte de testes completa, APK gerado pelo CI, roteiro de teste manual dos fluxos anteriores no aparelho.
8. **Documentação**: README, CHANGELOG, ROADMAP e docs da API atualizados.
9. **Riscos (§32)**: estado dos quatro riscos técnicos prioritários.
10. **Próxima fase**: pendências herdadas e riscos conhecidos registrados.

Enquanto a auditoria não estiver registrada e aprovada, a fase continua 🟡 na tabela "Estado atual".

## 5. Regras de escopo

- Não avançar para além da fase atual por conta própria; validar com o usuário (aparelho) antes de depender do que ainda não foi testado.
- Ao receber um pedido novo, encaixá-lo no ROADMAP antes de implementar.
- Manter `main` sempre utilizável.

## 6. Passagem entre agentes

Use `AgentsChat.md` para registrar entregas, perguntas, revisões e bloqueios, com autoria e evidências. Cada sessão deve deixar o próximo passo reproduzível; decisões de produto e progresso continuam no SPEC e ROADMAP. Leia as regras de colaboração em `AGENTS.md`.
