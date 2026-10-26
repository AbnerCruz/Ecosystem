# UC-51 / Issue #809 — Integridade das marcas de autoria e recuperação de execução

**Incidente reproduzido:** Urbe 2.0.0-beta.32; export de conversa de 26/10/2026. O agente tentou remover `{ia:"gpt-6-luna"}` oito vezes com `edit_note`: duas chamadas enviaram `new_text: ""` e receberam falso erro "falta new_text"; seis chamadas não alteraram o conteúdo. A tentativa de repetição também expôs um encerramento prematuro por `no_progress`.

**Ampliação do proprietário:** o próprio editor permite ao usuário remover as marcas, o que contraria a expectativa de autoria protegida. Não basta ensinar o modelo.

## Regras de proteção

1. `NoteMarks.PreservesAiAuthorship` impede que qualquer marca válida preexistente desapareça ou mude de identidade nos caminhos de gravação nativos. Novas marcas geradas pelo sistema continuam aceitas.
2. `NoteMarks.AllowsUserAuthorshipEdit` compara os tokens de autoria e o intervalo efetivamente alterado para bloquear exclusão, alteração, deslocamento e inserção manual da marca sem bloquear edições normais da prosa.
3. O editor Fonte valida na alteração síncrona de texto; o Visual valida a edição emitida por seus blocos; uma última verificação acontece antes de `SaveExistingTextFileAsync`. Uma tentativa rejeitada não deve tocar no vault.
4. O loop do assistente anuncia que as marcas são gerenciadas pelo Urbe e recusa tentativas de eliminá-las antes de exibir aprovações ou gravar. `edit_note` aceita string vazia como valor válido de `new_text`; uma exclusão de prosa comum continua funcionando.
5. Repetição idêntica sem progresso recebe **uma oportunidade limitada** para concluir ou mudar de plano; se persistir a mesma ação, encerra de modo controlado para não gastar tokens indefinidamente.

## Testes de regressão

- `AssistantAuthorshipRecoveryTests`: prompt, campo presente-vazio vs ausente, remoção e troca de modelos, edição legítima, falsificação manual, nenhuma gravação e continuação do agente.
- `EditorAuthorshipProtectionTests`: bloqueio imediato no editor Fonte com edição da prosa preservada e impossibilidade de forjar selo.
- `AssistantEfficiencyTests`: recuperação de um `noop` repetido e encerramento após insistência, sem chamadas indefinidas.

## Limites e verificações pendentes

**A marca continua sendo texto Markdown.** Uma edição efetuada fora do Urbe, diretamente no filesystem, não é impedida pelo aplicativo; não afirmar imutabilidade criptográfica ou proteção contra ferramentas externas. A proteção nativa também não autentica a identidade de marcas anteriores, nem pode reconstruir proveniência já apagada.

Testar no Android físico: Fonte, Visual, selecionar tudo, colar, selecionar trechos, Undo/Redo, alternar modos, salvar, reabrir, substituição integral por ferramentas da IA, histórico, importação e recuperação de falhas. O bloqueio deve preservar texto anterior e não introduzir saltos de caret ou latência perceptível. Antes de integrar, exigir gates .NET, Avalonia, Android e consistency. Qualquer introdução futura de um registro durável e à prova de adulteração exige desenho/migração e decisão de produto próprios, e não está implicitamente autorizada aqui.

## Pesquisa e critérios técnicos

- Avalonia `TextChanging` e `TextBox` permitem cancelar alterações durante a edição; usar uma regra objetiva no domínio sem chamadas de IA adicionais.
- Tool guardrails da OpenAI recomendam validar ações antes de efeitos colaterais, inclusive além de instruções de prompt.
- O SDK de agentes prevê a devolução de erros de ferramenta para continuidade; não tratar `noop` como sucesso de tarefa nem como motivo automático para abandonar trabalho independente.
- A análise da Anthropic recomenda controle de iterações e recuperação com evidências do ambiente.

Referências: https://docs.avaloniaui.net/docs/how-to/textbox-how-to ; https://developers.openai.com/api/docs/guides/agents/guardrails-approvals ; https://openai.github.io/openai-agents-js/guides/running-agents/ ; https://www.anthropic.com/research/building-effective-agents

Gates de release e DEVICE são independentes da publicação deste código. Não declarar concluído sem evidência (NN-017, NN-018).
