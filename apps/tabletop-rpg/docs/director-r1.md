# RPG-013 — Director: tensão e fios narrativos

## Escopo autorizado no roadmap

O Director é **local ao Product**, headless e determinístico. Gerencia
tensão (0–10), ameaças, pistas e promessas, propondo consequências ao
Mestre sem roteiro rígido. Não cria resultados canônicos nem usa
Model Gateway nesta etapa: não existem chamadas de IA ociosas.

### Uso

1. Código confiável cria `DirectorRuntime(campaign.Id, gmParticipantId)`.
2. O Mestre/host registra fios privados com `Track(new DirectorThread(...))`.
   Tipos: `Threat`, `Clue`, `Promise`. Não são fatos verdadeiros,
   missões ou conteúdo público.
3. **Somente após um evento**, o host passa `campaign.CreateGameMasterView(gmId)`
   a `Observe(view)`. A projeção autoriza e limita o contexto.
4. O Director consome sequências novas do histórico recente, calcula pressão,
   escolhe um fio relevante e emite `DirectorCue` privada.
5. O Mestre avalia a proposta e decide se a utiliza. Nenhuma
   `DirectorCue` executa uma intent, publica narração, revela fatos,
   manipula inventário, combate ou persiste na campanha.
6. `Resolve(key)` marca um fio como resolvido apenas no estado transitório
   do Director; não conclui uma `CampaignQuest` nem substitui as regras.

### Ritmo por evento

| Evento canônico | Tensão | Beat | Prioridade |
| --- | ---: | --- | --- |
| `scene-entered` | +1 | Foreshadow | Pista |
| `combat-start` | +3 | Escalation | Ameaça |
| `combat-attack` | +2 | Escalation | Ameaça |
| `skill-check` | +1 | Discovery | Pista |
| `quest-resolved` (Completed) | -3 | Consequence | Promessa |
| `quest-resolved` (Failed) | +3 | Consequence | Promessa |
| `combat-finished` | -2 | Consequence | Promessa |

A tensão é limitada a 0–10 e a pressão de cada fio também. A
escolha favorece tipo de fio compatível e alterna fios elegíveis:
**não existe sequência fixa de cenas**, final predefinido ou obrigação de
seguir uma sugestão. Outros eventos podem ser observados sem gerar cue.

### Integridade e limites

- `GameMasterView` é diferente de `PlayerView`; as sugestões
  permanecem privadas mesmo quando contêm informações secretas.
- Repetir a mesma observação não gera novos beats; um histórico que
  perdeu o último evento processado é rejeitado, não ignorado.
- O runtime atual recebe uma janela de 24 eventos. Se o host
  deixar passar mais eventos do que cabem nessa janela, precisa
  reconstruir um checkpoint; não há replay ilimitado silencioso.
- Estado do Director é **transitório nesta etapa**, sem formato de save,
  migração, novos campos no `Campaign`, servidor, UI ou provider.
  RPG-015 deverá tratar orquestração, retomada e budgets; não declarar
  reentrada perfeita ou sessão solo com IA completa.
- Registrar fio é operação manual/autorizada do host. O Director não
  infere automaticamente que qualquer fato secreto deva ser revelado.
- Somente o `GameMasterAgent` e seu fluxo de aprovação poderão
  transformar sugestões pertinentes em narração, respeitando RPG-012.

### Verificação

```sh
cd apps/tabletop-rpg
dotnet test --project tests/TabletopRpg.Core.Tests
```

Testes cobrem progressão por eventos, tensão/pressão, consequências,
isolamento, ausência de chamadas automáticas, determinismo, histórico
perdido, estados regressivos, limites e falta de mutação de campanha.

**Integração:** RPG-013 depende de RPG-012 (PR #517), que depende de
RPG-011 (PR #511). Permanecer `[~]` até CI e integração governada.
