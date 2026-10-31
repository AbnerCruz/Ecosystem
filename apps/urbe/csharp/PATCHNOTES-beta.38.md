# Urbe 2.0.0-beta.38 — correção de PDF do estúdio de páginas

Data: 31 de outubro de 2026
Canal: beta nativo Android / Windows
Origem: beta.37

## Correção

O comando **Exportar → Imprimir ou salvar PDF** do editor de páginas tinha dois problemas identificados no código: o WebView de impressão era liberado após um prazo arbitrário (sem verificar o estado real do trabalho) e o print manager recebia atributos vazios, ignorando o tamanho de papel escolhido no estúdio de livros.

- O WebView permanece disponível até o trabalho terminar, falhar ou ser cancelado. O monitoramento usa o thread principal em vez de timers associados a uma view desconectada.
- Para livros, os tamanhos A5, A4, Carta, 6 × 9 pol. e bolso são comunicados ao Android; para páginas comuns, A4 é o padrão.
- A origem do HTML permanece a mesma, com os arquivos locais embutidos; nenhuma mudança nos dados do vault.
- A impressão é iniciada após o carregamento da página. A exportação só é concluída quando o usuário confirma no diálogo nativo, não no momento de abri-lo.

## Testes e limites

- Testes de regressão para o contrato de impressão e o tamanho do papel.
- Validar em aparelho físico: PDF simples, documento com imagens, livro com capítulos, tamanho/margens, diálogo aberto por vários minutos e cancelamento.
- WebView do Android tem limitações conhecidas para CSS avançado de impressão, em particular paginação, cabeçalhos e rodapés; a correção não equivale a um compositor editorial dedicado.
- Instalação compatível com o canal beta existente: `app.urbe.csharp`; realizar backup antes de atualizar.

