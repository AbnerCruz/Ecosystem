# Urbe 2.0.0-beta.36 — selos de autoria administrados pelo Urbe

Data: 28 de outubro de 2026
Tag prevista: `urbe-csharp-v2.0.0-beta.36`
Canal: beta nativo (`app.urbe.csharp`, mesmo certificado da beta.35)
Base: `main` em `65f8011`, com a beta.35 (integração do Atlas, PR #841) já publicada.
Contrato: `apps/urbe/docs/csharp/AI-AUTHORSHIP-SEALS.md` (UC-51, CST-027 a CST-035).

## Selos `{ia:"modelo"}` — uma regra única

- **Cada bloco** que a IA cria ou reescreve recebe o próprio selo: título, parágrafo, item de lista, citação, tabela, código ou fórmula. Antes, um texto de vários parágrafos tinha um selo só, no fim.
- **Correção não é autoria.** Correções de ortografia, acentos, pontuação, formatação, uma ou duas palavras, junções e exclusões mantêm o autor original, humano ou IA.
- **Regenerar substitui o selo** pelo do modelo atual. Selos não se acumulam mais.
- **O agente não vê nem toca nos selos.**
  - Ele lê as notas sem os selos.
  - O que ele escrever em forma de selo é ignorado.
  - Tentar remover um selo não grava nada e não gera erro, então não há mais loop.
  - O `old_text` copiado da leitura volta a corresponder mesmo em notas com selos antigos no meio do texto.
- **Editor Visual:** a caixa de edição mostra o texto sem selo. O Urbe mantém o selo no bloco quando você edita, divide ou junta, e o remove só quando o bloco é apagado. Um selo digitado ou colado não vale.
- **Editor Fonte:** continua impedindo apagar, alterar ou forjar só o selo. Agora também protege a linha de selo depois de um bloco de código.
- **Aprovação com Editar:** você edita o texto sem selos. O que você acrescentou fica sem selo, e o que continua da IA mantém o dela. Se a nota mudar enquanto a aprovação espera, nada é sobrescrito.
- **Selos antigos fora do lugar**, como `** {ia:"…"}Páginas`, que quebrava o negrito, ou três selos repetidos, aparecem uma vez, no lugar certo, no editor e na exportação. O arquivo só muda no bloco que alguém editar.
- Arquivos que não são notas Markdown (`.json`, `.css`, `.js`) nunca recebem selo. Antes, `create_note` podia corromper um JSON de Personalização com a marca.

## Verificações

- Testes automatizados: Core e Client headless, 1 260 testes, todos aprovados localmente. Novos: `AiAuthorshipTests`, `AssistantAuthorshipSealTests` e casos novos em `EditorAuthorshipProtectionTests`.
- A FeaturesCheck real do proprietário (101 selos, 126 blocos) foi atribuída em ~3 ms, em headless no Linux. Reescrevê-la sem mudanças preserva todos os bytes, e uma edição altera só a linha editada.
- **Pendente no aparelho (DEVICE, NN-017):**
  - edição e divisão de bloco no Visual com Gboard;
  - colar, Desfazer e Refazer;
  - alternar Visual e Fonte;
  - aprovação editada;
  - sugestão inline.

  Conferir também que não há salto de caret nem latência perceptível. Não há alegação de validação física antes disso.

**Arquivos esperados:** `Urbe-CSharp-2.0.0-beta.36-Android.apk`, `Urbe-CSharp-2.0.0-beta.36-Windows-x64.zip`, `SHA256SUMS.txt`, `SHA256SUMS-Windows.txt`.
