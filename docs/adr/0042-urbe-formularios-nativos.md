# ADR-0042 — Formulários nativos e retomada explícita no Urbe

## Status

Proposto para integração. Implementa a direção expressamente solicitada pelo proprietário em 25/10/2026, Issue #762 / UC-90; não certifica Android físico.

## Contexto

O proprietário solicitou corrigir crashes recentes dos agentes e o segundo envio, depois implementar formulários conforme a proposta apresentada. O Urbe já possui executor, ferramentas estruturadas, conversas privadas e templates Markdown. O teste `Activity_InvalidModelPlanDoesNotCrashExpansionOrRefresh` reproduziu `InvalidOperationException` na conversão de um status booleano para string em `AssistantPanel.Activity.cs`. Isso comprova um caminho de crash; sem stack trace do aparelho não identifica conclusivamente o incidente relatado.

## Problema

Coletar múltiplos valores com validação, sem perguntas sequenciais, HTML executável, segundo runtime, aprovação de escrita implícita ou mistura entre conversas. Não perder uma segunda mensagem quando o primeiro turno está ativo.

## Opções

1. Prompts individuais em sequência: mantém a fricção relatada.
2. HTML/WebView gerados pelo modelo: contraria o cliente nativo e amplia confiança.
3. Definição declarativa e sessão no Core, controles Avalonia e executor existente.

## Decisão

Opção 3. `FormDefinition`, `FormField` e `FormSession` pertencem ao Urbe.Core. A capability local opcional `IAssistantForms` permite à ferramenta `request_form` solicitar uma sessão, persistida antes de retornar. A execução termina com `needs_input`, com resultados para chamadas posteriores não executadas. Não depende de uma Task viva: após reiniciar, o histórico reabre o formulário pendente. Enviar ou cancelar registra resposta estruturada na mesma conversa e inicia um novo turno do agente atual sob suas permissões vigentes. Não se promete replay de uma ferramenta interrompida nem execução com o processo morto.

Sessões de chat têm como única autoridade o arquivo privado da conversa, com ID, agente, conversa, chamada, definição, valores, estado e hash da nota de origem. Não vão para memória permanente. Se a origem mudar, o envio bloqueia e pede regeneração; o formulário nunca grava a nota sozinho. Edições posteriores continuam nas tools normais, com a política vigente e aprovação quando exigida.

Templates legados geram campos de texto automaticamente. Metadados opcionais em comentário `<!-- urbe-form JSON -->` descrevem tipos e condições; o comentário sai da nota produzida. Formulários podem ser salvos explicitamente como notas em `Modelos/`, com os valores atuais como padrões, sem registro paralelo. Rascunhos de templates ficam em arquivos privados `form-template-HASH.json`, separados dos chats, verificados por leitura e vinculados à versão do modelo. Cancelar não cria nota.

Campos incluem texto, texto longo, inteiros, números, data, hora, seleção, múltipla escolha, booleanos, escala, listas, notas, pastas, cor hexadecimal, soma declarativa e grupos repetíveis. Condições referenciam campos anteriores; cálculos não executam código. Profundidade e quantidade de controles são limitadas por segurança do renderizador. Não implementa anexos ou referências a grafos nesta versão.

A segunda mensagem entra numa fila privada por conversa. O executor consome cada mensagem somente após o turno anterior: não injeta nova fala no meio de um par tool_use/tool_result. Digitar faz o botão oferecer envio; o botão sem texto mantém a função Parar. A fila persiste no histórico e erros/cancelamento deixam os próximos pedidos preservados para retomada explícita.

## Consequências

Novo estado aditivo documentado em DATA-CATALOG, testes de compatibilidade, persistência falha, validação, cancelamento e isolamento. Renderizador mantém controles durante digitação. Planos malformados não podem derrubar a UI; logs grandes são paginados preservando conteúdo e cópia integral. Mudança de persistência é crítica e segue o integrador; não há migração destrutiva nem novas dependências.

## Alternativas rejeitadas

Não promover a plataforma: não há segundo consumidor demonstrado (NN-022). Não trocar o executor ou alterar providers/credenciais. Não interpretar código arbitrário enviado pelo modelo.

## Referências

MANIFEST §21–30; NN-001/008/009/010/011/016/017/018/019/020/022; ADR-0032/0033/0041; UC-44/89/90; Issue #762; `apps/urbe/docs/csharp/ASSISTANT-FORMS.md`.
