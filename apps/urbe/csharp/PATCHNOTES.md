# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.36
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** exclusivamente pelo workflow oficial, após a integração e os gates
**Identidade Android:** app.urbe.csharp

> Este arquivo descreve a beta.36 preparada. Não confirma a publicação: confira se existe a tag `urbe-csharp-v2.0.0-beta.36` com o APK Android assinado e o ZIP Windows. O histórico fica em `PATCHNOTES-HISTORY.md`.

## Selos de autoria da IA administrados pelo Urbe

- Cada bloco que a IA cria ou reescreve recebe o próprio selo `{ia:"modelo"}`, não só o último. Regenerar substitui o selo, sem acumular.
- Correções de ortografia, pontuação, formatação ou poucas palavras mantêm o autor original, e o seu texto corrigido continua sendo seu.
- O agente lê as notas sem selos e não consegue criar, remover nem mover um selo. Isso acaba com o loop de "limpar a marca" e com as edições que falhavam por correspondência exata.
- No editor Visual, você edita o texto sem ver o selo, e o Urbe o mantém no bloco. No editor Fonte, apagar ou forjar só o selo continua impedido.
- Selos antigos repetidos ou no meio do negrito aparecem corrigidos no editor e na exportação.
- Arquivos que não são notas (`.json`, `.css`, `.js`) nunca recebem selo.

## Compatibilidade e distribuição

- A identidade, a assinatura beta e os arquivos existentes não mudam. A atualização instala por cima da beta.35. Os bytes de uma nota só mudam nos blocos editados.
- É preciso CI, integração aprovada e validação no aparelho. Faça backup antes de atualizar.

**Detalhes técnicos e testes:** `PATCHNOTES-beta.36.md` e o contrato `apps/urbe/docs/csharp/AI-AUTHORSHIP-SEALS.md`.
