# ADR-0037 — Urbe: execução continuada e qualidade adaptativa do Assistente

## Status

Proposto para revisão — pedido explícito do proprietário em 24/10/2026. A existência de código e PR não substitui gates, validação no Android nem aprovação arquitetural quando exigida.

## Contexto

O Assistente nativo do Urbe ainda depende da atividade Android para trabalhos iniciados no chat, não possui sinalização de estados fora do app e impõe tetos arbitrários ao raciocínio operacional: 30 rodadas por execução, 12 no subagente e 20 itens no plano. A ferramenta ask_user trabalha com uma pergunta por vez, mas a orientação privilegia encerramento rápido. Uma ficha de personagem sem conteúdo anterior mostrou ausência de descoberta, arquitetura do artefato, revisão textual e manutenção de notas pertinentes, inclusive ideias paralelas mencionadas pelo usuário.

## Decisão proposta

1. **Processo Android.** O trabalho iniciado voluntariamente enquanto a Activity está visível registra seu estado na interface `PlatformAssistantActivity`. O host Android mantém serviço de primeiro plano do tipo `specialUse`, notificação persistente enquanto executa ou aguarda aprovação e notificações de pergunta, aprovação, conclusão e falha quando a Activity está em segundo plano. O serviço não contém ferramentas de vault, credenciais nem segunda implementação de agente; o `AssistantPanel` continua responsável por executar `Urbe.Core.Assistant.AssistantAgent`. A notificação abre o próprio Assistente. Android 13+ solicita permissão de notificação somente após gesto do usuário que inicia uma tarefa.
2. **Sem promessa de imortalidade.** O serviço evita a suspensão comum por ocultar o app; `force-stop`, reinício, falta de recursos ou morte do processo podem interromper execução. Não se recria automaticamente agente desprovido de suas dependências. Retomada robusta requer uma nova fatia: journal privado durável, serialização do ponto de execução, pendências, reconciliação idempotente de operações, recovery de credenciais e do vault e confirmação segura antes de retomar escritas. UI deve comunicar esta limitação. Se o sistema negar o serviço, o trabalho ainda funciona no app aberto, mas continuidade em background não fica garantida.
3. **Perguntas e plano adaptativos.** O motor não limita automaticamente quantas rodadas, perguntas ou etapas o agente pode usar; limites explícitos continuam disponíveis apenas para test harnesses ou chamadas legadas. Conservam-se cancelamento, detecção de chamadas que falham repetidas vezes, permissões e aprovações. A ferramenta `update_plan` não trunca vinte etapas. O agente escolhe rigor proporcional à complexidade e ao risco, inspeciona o vault antes de perguntar e não repete dúvidas respondidas.
4. **Qualidade dos artefatos.** Para trabalhos criativos ou de informação insuficiente, distinguir fatos do vault, inferências, propostas e decisões do usuário; estruturar artefatos para o uso final; trabalhar prosa interessante e pertinente; relacionar e atualizar documentos conectados quando autorizado. Ideias paralelas pertinentes são pesquisadas e podem virar notas ligadas, mas expansões grandes pedem aprovação do usuário. Após edição de notas, uma etapa de verificação é induzida no loop de agente para reler o que foi gravado e rever completude, coerência e qualidade antes da entrega.

## Segurança e consequências

`specialUse` requer `FOREGROUND_SERVICE_SPECIAL_USE` e metadado declarativo no manifesto; eventual distribuição Play Store dependerá de avaliação dos usos declarados. Não usar uma modalidade falsa (`microphone` ou `dataSync`) para computação de agente. A notificação não revela dados privados do vault, credenciais, prompt nem conteúdo da pergunta; apenas indica o estado. O aplicativo deve oferecer parada voluntária e manter aprovação para ações relevantes. Sem limite arbitrário de passos, custo de API e duração podem crescer; o usuário mantém interrupção e o sistema conserva freios de loop, além da necessidade futura de controles de custo/progresso baseados em orçamento voluntário, sem cota oculta imposta pela ferramenta. Perguntas proporcionais não significam interrogatório obrigatório.

## Alternativas rejeitadas

- `Task.Run` sozinho: não preserva processo quando Activity entra em background.
- WorkManager como substituto direto: adequado para jobs reagendáveis/idempotentes, não para um loop interativo que depende de aprovações e mudanças locais.
- Forçar um número grande de etapas (por exemplo, 100): apenas desloca o corte arbitrário e incentiva respostas superficiais.
- Criar um segundo Agent Runtime ou armazenar credenciais no serviço: duplica autoridade e aumenta riscos, contrariando ADR-0033.

## Validação e próximos gates

Testes headless para planos longos, turnos numerosos e perguntas; CI Android de compilação; aparelho Android 13 para tarefa longa → app minimizado → bloqueio de tela → notificação de aprovação/pergunta → retorno ao Assistente e conclusão. Testar negação de POST_NOTIFICATIONS, restrições de bateria, parada forçada e recuperação após morte do processo (esta última ainda não suportada). Não liberar versão como execução recuperável até a fatia de journal/resume ser concluída.

## Referências

MANIFEST NN-001, NN-005, NN-008, NN-010, NN-011, NN-016, NN-017, NN-022; ADR-0032, ADR-0033; apps/urbe/docs/csharp/ROADMAP.md; documentação Android Developers sobre tipos de serviço em primeiro plano e specialUse.
