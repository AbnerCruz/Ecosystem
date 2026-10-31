# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.38
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** exclusivamente pelo workflow oficial, após a integração e os gates
**Identidade Android:** app.urbe.csharp (mesmo certificado da beta.37)

> Esta nota descreve a versão preparada. A publicação exige a release `urbe-csharp-v2.0.0-beta.38` com APK assinado. O histórico das versões anteriores fica em `PATCHNOTES-HISTORY.md`.

## Exportação PDF — editor de páginas

- Corrigido o ciclo de vida do WebView de impressão: o Android não destrói o documento enquanto o serviço de impressão está trabalhando ou enquanto o usuário escolhe onde salvar.
- Os livros exportam com a mídia correspondente à configuração do editor: A5, A4, Carta, 6 × 9 polegadas e bolso (110 × 180 mm).
- Páginas comuns usam A4 por padrão; o usuário ainda pode escolher outro papel no diálogo de impressão do sistema.
- A exportação mantém o mesmo HTML do editor, incluindo imagens incorporadas do vault; não altera as páginas ou as notas salvas.
- Regressões automatizadas verificam a seleção do papel e o contrato de envio ao sistema de impressão.

## Compatibilidade e validação

- Instala por cima da beta.37, mantendo a identidade `app.urbe.csharp`; faça backup antes de atualizar.
- O sistema Android continua sendo responsável por concluir e salvar o PDF após a escolha de “Salvar como PDF”.
- A paginação final e a renderização no aparelho Android ainda exigem validação manual com documentos simples, livros extensos e imagens.
- Permanecem as funcionalidades da beta.37. Detalhes: `PATCHNOTES-beta.37.md` e `PATCHNOTES-beta.38.md`.
