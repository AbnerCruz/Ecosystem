# Urbe 2.0.0-beta.33 — integridade da autoria e execução da IA

**Canal:** nativo Android e Windows, identidade Android `app.urbe.csharp`.
**Status:** corte preparado; a publicação exige integração governada, CI e workflow oficial. A validação tátil no Android permanece independente.

## Marcas de autoria

- O selo `{ia:"modelo"}` é administrado pelo Urbe e não deve ser removido, alterado ou falsificado pelo editor Fonte, pelo editor Visual ou por um agente.
- Tentativas de editar um selo são recusadas no editor e novamente antes da gravação no vault. Alterações normais à prosa continuam permitidas.
- **Desfazer** uma operação registrada da IA continua funcionando: uma reversão da transação inteira, verificada pela versão atual, pode retirar o selo daquela operação junto com o texto; nenhuma edição comum recebe essa exceção.
- Não se alega proteção contra programas externos alterando diretamente o arquivo `.md` no filesystem, nem proveniência assinada digitalmente.

## Agentes: menos tentativas inúteis

- O runtime informa explicitamente que selos são protegidos. Tentativas de removê-los não chegam à gravação nem a uma aprovação desnecessária.
- `edit_note` distingue `new_text: ""` (substituição vazia legítima) de um argumento ausente. Campos obrigatórios não relacionados continuam validados.
- Um ciclo repetido sem progresso recebe uma oportunidade limitada de alterar o plano ou responder; persistindo a repetição, termina com motivo explícito, sem consumir chamadas ilimitadas.

## Compatibilidade, dados e testes

- Mantidos o formato Markdown, a pasta do usuário, identidade e certificado beta existentes. A versão 2.0.0-beta.33 atualiza por cima da beta.32 no mesmo canal.
- Regressões em `AssistantAuthorshipRecoveryTests`, `AssistantEfficiencyTests`, `AssistantStoreTests`, `EditorAuthorshipProtectionTests` e suítes anteriores de edição/undo.
- A publicação oficial não equivale a teste no aparelho: conferir teclado, selecionar tudo, colar, undo, modo Visual e Fonte, salvar e reabrir, e estabilidade de desempenho.

Issue #809: https://github.com/AbnerCruz/Ecosystem/issues/809
PR #810: https://github.com/AbnerCruz/Ecosystem/pull/810
Análise: `apps/urbe/docs/csharp/ASSISTANT-AUTHORSHIP-INTEGRITY-20261026.md`.
