# Formulários inteligentes — UC-90

Direção solicitada pelo proprietário em 25/10/2026. Rastreio: Issue #762 e ADR-0042. O estado de verificação fica no handoff e no CI, sem equivalência automática com Android físico.

## Como usar

- Peça ao agente para ler uma nota e coletar os dados num formulário. `request_form` apresenta os campos no chat e aguarda envio explícito. `ask_user` continua para uma dúvida simples.
- Campos oferecem valores iniciais, ajuda, agrupamentos, obrigatoriedade e validação. O usuário pode sair do painel e retomar pelo histórico do mesmo agente; os valores ficam nos dados privados da conversa.
- Enviar devolve JSON estruturado ao solicitante; cancelar devolve estado cancelado. Nenhum dos dois autoriza escrita nem memoriza respostas automaticamente. O modelo mantém suas permissões, prévias, aprovação e desfazer existentes.
- Escolher uma nota em `Modelos/` abre um formulário completo em vez de pedir cada placeholder separadamente. Sem metadados, `{{campo}}` vira texto, mantendo os padrões `{{titulo}}` e `{{data}}`.
- `Salvar como modelo reutilizável` salva a definição e os valores atuais como padrões de uma nota em `Modelos/`. O mesmo fluxo de criação reutiliza esse formulário, sem IA para descobrir os placeholders.
- A nota vinculada é verificada por hash no envio. A primeira escrita desse turno verifica novamente a versão; escritas subsequentes acompanham a versão escrita pelo próprio turno. Mudança externa bloqueia a operação, com erro visível e necessidade de gerar novo formulário.

## Contrato local

`FormDefinition` versão 1 tem title, description, source_path opcional e fields. Cada campo possui id estável, label, type, description, group, required, initial, options, min/max. `visible_when` + `equals_value` referenciam um campo anterior. `sum` calcula soma de campos anteriores sem interpretar código. Grupos `repeat` contêm `fields` próprios e arrays de objetos como valores.

Tipos: text, long_text, integer, number, date, time, select, multi_select, checkbox, toggle, scale, list, note, folder, color, calculated e repeat. Datas/horas usam seletores nativos Avalonia; múltipla escolha usa ListBox com virtualização de opções. Cor é entrada hexadecimal validada. Listas têm um item por linha. Anexos e referências a grafos não fazem parte deste contrato.

```markdown
<!-- urbe-form {"title":"Ficha","fields":[{"id":"nome","label":"Nome","type":"text","required":true},{"id":"idade","label":"Idade","type":"integer","min":0}]} -->
# {{titulo}}

Nome: {{nome}}
Idade: {{idade}}
```

O comentário de metadados não aparece na nota criada. Substituições são literais, sem interpretar placeholders ou código dentro das respostas.

## Persistência e recuperação

Chat: `conversa-*.json` contém forms, cada um com identidade de agente/conversa/chamada, definition, values, state e source_hash. A sessão é gravada e relida antes de a tool retornar. A execução para em `needs_input`, não mantém uma espera volátil. Reabrir o histórico recupera a solicitação. Não há execução após o Android matar o processo; a continuação começa com uma ação explícita do usuário.

Templates: rascunho privado `form-template-HASH.json`, ligado a vault/modelo/nome/versão do conteúdo. Cancelamento remove o rascunho e não cria arquivo no vault. Mudança do modelo recusa reusar valores contra um layout diferente. Arquivos de notas existentes não são migrados.

## Segundo envio e estabilidade

Enquanto há trabalho ativo, uma mensagem nova entra em `queuedMessages` da mesma conversa, com anexos. O executor termina o turno atual e processa a próxima mensagem. Falha ou cancelamento preservam a fila e oferecem retomada explícita. O botão com texto envia/enfileira; sem texto, durante a execução, oferece Parar. Não há segundo executor simultâneo na mesma conversa.

Planos gerados por modelos são dados não confiáveis: tipos inesperados não podem lançar exceção na UI. Argumentos e resultados grandes usam páginas de 12 mil caracteres, com cópia integral disponível. Controles de formulário permanecem montados enquanto se digita; o teclado não é aberto automaticamente.

## Verificação e limites

Testes cobrem crash por plano malformado, segundo envio real, validação/condições/soma/grupos, persistência verificada, modelos legados e typed, rascunho/retomada e resposta do formulário. CI faz regressão Core/cliente e builds nativos. IME, toque, rolagem, reinício do processo e desempenho precisam de Android físico. O crash reproduzido é um caminho confirmado no código; não substitui o stack trace do incidente do proprietário.

Sem editor visual geral de definições ou associação arbitrária de formulários a comandos/plugins neste incremento. A reutilização operacional acontece por templates Markdown e pela ferramenta dos agentes. Essa extensão permanece no roadmap.

## Eficiência do executor — adendo solicitado em 25/10/2026

O cliente nativo ativa `OptimizeToolUse` no executor e nas consultas delegadas. A compatibilidade do oráculo JS continua congelada, sem impor seus comportamentos ineficientes ao Urbe nativo.

- Escritas de nota/página cuja prévia é idêntica ao arquivo atual terminam como `noop` antes de autorização, watermark, disco, histórico/desfazer e auditoria paga de alterações. O texto editado pelo usuário na aprovação também é comparado antes de gravar.
- Leituras determinísticas de documentos e contratos de página têm identidade canônica de argumentos e versão do DocumentStore. Chamadas idênticas simultâneas compartilham a mesma execução; alteração no vault invalida a reutilização, inclusive após um erro anterior. Rede, memória e UI não são cacheadas por essa regra.
- Resultados integrais continuam no histórico. No request do modelo, cópias estritamente idênticas de evidências de leitura viram referências apenas quando a primeira cópia permanece integralmente na MESMA requisição. A projeção é recalculada após cortes de contexto; nenhuma referência aponta para um resultado descartado. O orçamento avalia a projeção real, sem pagar o custo de duplicação para depois expulsar contexto útil.
- `max_lines` no read_note nativo passa a delimitar o trecho efetivamente solicitado. Um trecho completo não obriga ler o restante da nota; truncamento físico dentro do trecho continua explícito. O guia do executor deixa de ordenar leitura integral e reenvio de plano/schema como rituais.
- Um estado operacional exatamente repetido, sem leitura nova ou mutação, é sinalizado como ciclo sem progresso. Não é um teto de chamadas/etapas/tokens: o estado `no_progress` é visível e permite continuidade explícita. Nunca anuncia tarefa concluída por ter interrompido o modelo.
- Cada execução registra contagens de leituras reutilizadas e gravações sem mudança evitadas, além de caracteres redundantes não reenviados. São medidas determinísticas de execução/contexto, não uma promessa de economia percentual de tokens ou faturamento real do provedor.

Testes: AssistantEfficiencyTests cobre execução única em leituras paralelas, invalidação por versão, no-op sem aprovação/disco/watermark/revisão extra, trechos exatos, ciclo estável e integridade das referências no contexto. O mecanismo não é uma solução universal de custo: descoberta progressiva de schemas e uma política de atualização de fontes externas exigem contratos próprios antes de serem anunciadas.
