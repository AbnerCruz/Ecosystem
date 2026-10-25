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
